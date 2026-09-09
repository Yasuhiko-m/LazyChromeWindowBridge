// Profile-global browser metadata only. No download mutation, URL collection or file access.
const KEY = 'download-outbox';
export const DOWNLOAD_LIMITS = Object.freeze({ tracked: 128, pending: 256, bridges: 16 });
const keyOf = value => value.bridgeId + '|' + value.origin;
const terminal = state => state === 'complete' ? 'Complete' : state === 'interrupted' ? 'Interrupted' : null;

export class DownloadManager {
  constructor(browser, bindings, request = globalThis.fetch.bind(globalThis)) {
    this.browser = browser; this.bindings = bindings; this.request = request;
    this.writes = Promise.resolve();
  }
  async load() {
    return (await this.browser.storage.session.get(KEY))[KEY] ?? { sequence: 0, tracked: [], pending: [] };
  }
  async persist(state) {
    while (state.tracked.length > DOWNLOAD_LIMITS.tracked || state.pending.length > DOWNLOAD_LIMITS.pending) {
      // Explicit overflow boundary: retire a whole oldest terminal stream, then oldest active.
      const retired = state.tracked.find(t => t.state !== 'Created') ?? state.tracked[0];
      state.tracked = state.tracked.filter(t => t.id !== retired.id);
      state.pending = state.pending.filter(e => e.event.downloadId !== retired.id);
    }
    const snapshot = structuredClone(state);
    const write = this.writes.then(() => this.browser.storage.session.set({ [KEY]: snapshot }));
    this.writes = write.catch(() => {});
    await write;
  }
  async representatives() {
    const groups = new Map();
    for (const record of (await this.bindings.records()).sort((a, b) => a.appSessionId.localeCompare(b.appSessionId))) {
      if (!record.downloads || record.closed || record.navigationPending || record.nativePending) continue;
      const key = keyOf(record);
      if (!groups.has(key) && groups.size < DOWNLOAD_LIMITS.bridges) groups.set(key, record);
    }
    return groups;
  }
  append(state, tracked, lifecycle, filename, error, observedAt, resolve = false) {
    if (state.sequence >= Number.MAX_SAFE_INTEGER) throw Error('Download sequence capacity reached.');
    state.pending.push({ sequence: ++state.sequence, targets: structuredClone(tracked.targets), resolve,
      event: { downloadId: tracked.id, state: lifecycle, filename, error, observedAt } });
    tracked.state = lifecycle;
  }
  async created(item) {
    if (item.incognito || !Number.isInteger(item.id) || item.id < 0 ||
        typeof item.filename !== 'string' || item.filename.length > 1024 || item.filename.includes('\0')) return;
    const groups = await this.representatives();
    if (!groups.size) return;
    const state = await this.load();
    if (state.tracked.some(t => t.id === item.id)) return;
    const tracked = { id: item.id, state: 'Created', targets: [...groups.values()].map(r =>
      ({ bridgeId: r.bridgeId, origin: r.origin, browserSessionId: r.browserSessionId })) };
    state.tracked.push(tracked);
    const observedAt = new Date().toISOString();
    this.append(state, tracked, 'Created', item.filename, null, observedAt);
    if (terminal(item.state)) this.append(state, tracked, terminal(item.state), null, null, observedAt, true);
    await this.persist(state); // Durable before any attempt/acknowledgement.
    await this.flush(state, groups);
  }
  async changed(delta) {
    const lifecycle = terminal(delta.state?.current);
    if (!lifecycle) return;
    const state = await this.load(), tracked = state.tracked.find(t => t.id === delta.id);
    if (!tracked || tracked.state === 'Complete' || tracked.state === lifecycle) return;
    const reason = typeof delta.error?.current === 'string' && delta.error.current.length <= 128 ? delta.error.current : null;
    this.append(state, tracked, lifecycle, null, lifecycle === 'Interrupted' ? reason : null, new Date().toISOString(), true);
    await this.persist(state); // Retain the transition even if full-item search or HTTP fails.
    await this.flush(state, await this.representatives());
  }
  async reconcile() {
    const state = await this.load();
    // Only IDs already observed in this browser session; never search arbitrary history.
    for (const tracked of [...state.tracked]) {
      if (tracked.state === 'Complete' || !state.tracked.includes(tracked)) continue;
      try {
        const [item] = await this.browser.downloads.search({ id: tracked.id });
        const lifecycle = terminal(item?.state);
        if (lifecycle && lifecycle !== tracked.state) {
          this.append(state, tracked, lifecycle, null, null, new Date().toISOString(), true);
          await this.persist(state);
        }
      } catch { /* Keep the known ID; the existing alarm retries. */ }
    }
    await this.flush(state, await this.representatives());
  }
  async flush(state, groups) {
    for (const entry of state.pending) {
      if (!entry.resolve) continue;
      try {
        const [item] = await this.browser.downloads.search({ id: entry.event.downloadId });
        if (!item || item.incognito || typeof item.filename !== 'string' || item.filename.length > 1024 || item.filename.includes('\0')) continue;
        const error = entry.event.state === 'Interrupted' ? (item.error ?? entry.event.error ?? null) : null;
        if (error !== null && (typeof error !== 'string' || error.length > 128)) continue;
        entry.event.filename = item.filename; entry.event.error = error; entry.resolve = false;
        await this.persist(state);
      } catch { /* The unresolved event remains in the outbox. */ }
    }
    // A failing destination cannot block another bridge; each bridge keeps sequence order.
    await Promise.all([...groups].map(async ([key, record]) => {
      let sent = 0;
      for (const entry of state.pending) {
        const target = entry.targets.find(t => keyOf(t) === key);
        if (!target) continue;
        if (entry.resolve || sent++ >= 16) break;
        try {
          const response = await this.request(record.origin + '/lazy-chrome-window-bridge/api/downloads', {
            method: 'POST', headers: { Authorization: 'Bearer ' + record.token,
              'X-Bridge-Session': record.appSessionId, 'Content-Type': 'application/json' },
            body: JSON.stringify({ bridgeId: target.bridgeId, browserSessionId: target.browserSessionId,
              sequence: entry.sequence, event: entry.event }),
            signal: AbortSignal.timeout(3000), redirect: 'error', credentials: 'omit', cache: 'no-store'
          });
          if (!response.ok) break;
          entry.targets = entry.targets.filter(t => keyOf(t) !== key);
          // A restart before this durable acknowledgement retries the same sequence safely.
          await this.persist(state);
        } catch { break; }
      }
    }));
    state.pending = state.pending.filter(e => e.targets.length);
    await this.persist(state);
  }
}
