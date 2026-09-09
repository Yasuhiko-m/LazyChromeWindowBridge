const PREFIX = "/lazy-chrome-window-bridge";
const KEY = "binding:";
const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

export class BindingManager {
  constructor(browser, request = globalThis.fetch.bind(globalThis), monitor = null) { this.browser = browser; this.request = request; this.monitor = monitor; }

  async records() {
    const data = await this.browser.storage.session.get(null);
    return Object.entries(data).filter(([key]) => key.startsWith(KEY)).map(([, value]) => value);
  }
  save(record) { return this.browser.storage.session.set({ [KEY + record.appSessionId]: record }); }
  remove(record) { this.monitor?.remove(record.appSessionId); return this.browser.storage.session.remove(KEY + record.appSessionId); }
  async browserIdentity() {
    const { browserSessionId } = await this.browser.storage.session.get("browserSessionId");
    if (browserSessionId) return browserSessionId;
    const identity = crypto.randomUUID();
    await this.browser.storage.session.set({ browserSessionId: identity });
    return identity;
  }
  async requestSession(record, action) {
    const response = await this.request(`${record.origin}${PREFIX}/api/sessions/${record.appSessionId}${action ? "/" + action : ""}`, {
      method: action ? "POST" : "GET",
      headers: { Authorization: `Bearer ${record.token}`, ...(action ? { "Content-Type": "application/json" } : {}) },
      ...(action ? { body: JSON.stringify({ browserSessionId: record.browserSessionId, windowId: record.windowId, tabId: record.tabId }) } : {}),
      signal: AbortSignal.timeout(3000), redirect: "error", credentials: "omit", cache: "no-store"
    });
    if (!response.ok) {
      const error = new Error(`Caller rejected session (${response.status}).`);
      error.terminal = response.status === 401 || response.status === 409;
      throw error;
    }
    return action ? null : response.json();
  }
  async bootstrap(message, sender) {
    const url = new URL(message.url);
    if (sender.id !== this.browser.runtime.id || sender.frameId !== 0 || !sender.tab || sender.tab.incognito ||
        sender.url !== url.href || url.protocol !== "http:" || url.hostname !== "127.0.0.1" ||
        !url.port || url.username || url.password || url.pathname !== PREFIX + "/bootstrap")
      throw new Error("Invalid bootstrap sender.");
    const fragment = new URLSearchParams(url.hash.slice(1));
    const appSessionId = fragment.get("session"), bridgeId = fragment.get("bridge"), token = fragment.get("token");
    if (fragment.get("v") !== "1" || !GUID.test(appSessionId ?? "") || !GUID.test(bridgeId ?? "") || !/^[A-F0-9]{64}$/.test(token ?? ""))
      throw new Error("Invalid bootstrap capability.");
    if (url.search && url.search !== `?session=${appSessionId}`) throw new Error("Invalid bootstrap marker.");
    const records = await this.records();
    const existing = records.find(record => record.appSessionId === appSessionId);
    if (existing) {
      if (existing.windowId !== sender.tab.windowId || existing.tabId !== sender.tab.id || existing.token !== token || existing.origin !== url.origin)
        throw new Error("This session already owns a different window.");
      await this.reconcileRecord(existing);
      return;
    }
    if (records.length >= 256 || records.some(record => !record.closed && record.windowId === sender.tab.windowId))
      throw new Error("Window already bound or session limit reached.");
    const record = { appSessionId, bridgeId, token, origin: url.origin, bootstrapUrl: url.href,
      browserSessionId: await this.browserIdentity(), windowId: sender.tab.windowId, tabId: sender.tab.id,
      launchUrl: null, navigationPending: true, closed: false };
    const description = await this.requestSession(record);
    if (description.appSessionId !== appSessionId || description.bridgeId !== bridgeId ||
        !["http:", "https:"].includes(new URL(description.launchUrl).protocol)) throw new Error("Caller identity mismatch.");
    record.launchUrl = description.launchUrl;
    record.nativePending = description.nativeGeometry === true;
    record.monitoring = description.monitoring === true;
    // Persist before acknowledging to the caller; a worker restart can replay the idempotent bind.
    await this.save(record);
    await this.reconcileRecord(record);
  }
  async reconcileRecord(record) {
    try {
      if (!record.closed) {
        try { await this.browser.windows.get(record.windowId); }
        catch { record.closed = true; record.closedAt = Date.now(); await this.save(record); }
      }
      await this.requestSession(record, record.closed ? "closed" : "bind");
      if (record.closed) { await this.remove(record); return; }
      if (record.nativePending) {
        await this.requestSession(record, "native");
        record.nativePending = false;
        await this.save(record);
      }
      if (record.navigationPending) {
        let tab;
        try { tab = await this.browser.tabs.get(record.tabId); } catch { /* The window, not this tab, owns the session. */ }
        // URL equality only protects the one-time bootstrap transition. It never decides window ownership.
        if (tab?.windowId === record.windowId && tab.url === record.bootstrapUrl)
          await this.browser.tabs.update(record.tabId, { url: record.launchUrl });
        record.navigationPending = false;
        await this.save(record);
      }
      this.monitor?.ensure(record);
    } catch (error) {
      if (error.terminal || (record.closed && Date.now() - record.closedAt > 300000)) await this.remove(record);
      throw error;
    }
  }
  async reconcile() {
    // Each record uses its own storage key, so parallel I/O cannot overwrite another session.
    await Promise.all((await this.records()).map(record => this.reconcileRecord(record).catch(() => {})));
  }
  async windowRemoved(windowId) {
    const matches = (await this.records()).filter(record => record.windowId === windowId);
    for (const record of matches) {
      record.closed = true;
      record.closedAt = Date.now();
      await this.save(record); // Keep an outbox if the loopback server is temporarily unavailable.
      try { await this.reconcileRecord(record); } catch { /* Alarm retries the persisted outbox. */ }
    }
  }
}
