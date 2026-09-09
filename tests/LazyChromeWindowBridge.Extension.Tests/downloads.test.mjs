import test from 'node:test';
import assert from 'node:assert/strict';
import { DownloadManager, DOWNLOAD_LIMITS } from '../../src/LazyChromeWindowBridge.Extension/downloads.js';

function fixture(sessionCount = 1) {
  const data = {}, items = new Map(), searches = [], requests = [], delivered = [], marks = new Map();
  const bridgeId = crypto.randomUUID(), browserSessionId = crypto.randomUUID();
  const records = Array.from({ length: sessionCount }, (_, i) => ({ appSessionId: crypto.randomUUID(), bridgeId,
    browserSessionId, origin: 'http://127.0.0.1:45678', token: 'A'.repeat(64), downloads: true,
    closed: false, navigationPending: false, nativePending: false, windowId: i + 1 }));
  let offline = false, lookupFails = false, loseAck = false, offlineBridge = null;
  const browser = { storage: { session: {
    async get(key) { return structuredClone({ [key]: data[key] }); },
    async set(value) { Object.assign(data, structuredClone(value)); }
  } }, downloads: { async search(query) {
    assert.deepEqual(Object.keys(query), ['id']); searches.push(query.id);
    if (lookupFails) throw Error('search unavailable');
    return items.has(query.id) ? [structuredClone(items.get(query.id))] : [];
  } } };
  const request = async (url, options) => {
    const body = JSON.parse(options.body);
    assert(data['download-outbox'].pending.some(e => e.sequence === body.sequence), 'persist before send');
    assert(!url.includes(records[0].token));
    assert.equal(options.redirect, 'error'); assert.equal(options.credentials, 'omit');
    assert.equal(options.headers.Authorization.length, 71);
    requests.push({ url, body, representative: options.headers['X-Bridge-Session'] });
    if (offline || offlineBridge === body.bridgeId) throw Error('temporary loopback failure');
    if (body.sequence > (marks.get(body.bridgeId) ?? 0)) { marks.set(body.bridgeId, body.sequence); delivered.push(body); }
    if (loseAck) { loseAck = false; throw Error('acknowledgement lost'); }
    return { ok: true };
  };
  const manager = () => new DownloadManager(browser, { records: async () => structuredClone(records) }, request);
  const item = (id = 1, state = 'in_progress') => ({ id, state, filename: 'C:\\Downloads\\neutral-' + id + '.zip', incognito: false });
  return { data, items, searches, requests, delivered, records, manager, item,
    setOffline: v => { offline = v; }, setLookupFails: v => { lookupFails = v; }, loseNextAck: () => { loseAck = true; },
    offlineDestination: v => { offlineBridge = v; } };
}

test('Created is durable before delivery and exposes only the requested public metadata', async () => {
  const f = fixture(), item = { ...f.item(), filename: '', url: 'https://private.invalid/', referrer: 'private' };
  await f.manager().created(item);
  const value = f.delivered[0].event;
  assert.deepEqual(Object.keys(value).sort(), ['downloadId', 'state', 'filename', 'error', 'observedAt'].sort());
  assert.equal(value.state, 'Created'); assert.equal(value.filename, ''); assert.equal(value.error, null);
  assert(Number.isFinite(Date.parse(value.observedAt))); assert.equal(f.data['download-outbox'].pending.length, 0);
  assert(!JSON.stringify(f.data['download-outbox']).includes('private.invalid'));
});
test('Complete resolves exact official current filename rather than incomplete delta', async () => {
  const f = fixture(), m = f.manager(); await m.created(f.item());
  f.items.set(1, { ...f.item(1, 'complete'), filename: 'C:\\Downloads\\exact-資料 (1).zip' });
  await m.changed({ id: 1, state: { current: 'complete' }, filename: { previous: 'incorrect' } });
  assert.deepEqual(f.delivered.map(e => e.event.state), ['Created', 'Complete']);
  assert.equal(f.delivered[1].event.filename, f.items.get(1).filename);
  assert.equal(f.delivered[1].event.error, null); assert.deepEqual(f.searches, [1]);
});
test('Interrupted propagates the official reason and filename', async () => {
  const f = fixture(), m = f.manager(); await m.created(f.item());
  f.items.set(1, { ...f.item(1, 'interrupted'), error: 'NETWORK_FAILED' });
  await m.changed({ id: 1, state: { current: 'interrupted' } });
  assert.equal(f.delivered[1].event.state, 'Interrupted'); assert.equal(f.delivered[1].event.error, 'NETWORK_FAILED');
  assert.equal(f.delivered[1].event.filename, f.items.get(1).filename);
});
test('five sessions of one bridge produce one lifecycle stream', async () => {
  const f = fixture(5), m = f.manager(); await m.created(f.item());
  f.items.set(1, f.item(1, 'complete')); await m.changed({ id: 1, state: { current: 'complete' } });
  assert.equal(f.requests.length, 2); assert.equal(f.delivered.length, 2);
  assert(f.delivered.every(e => !('appSessionId' in e.event)));
});
test('lost acknowledgement retries same sequence without duplicate consumer delivery', async () => {
  const f = fixture(); f.loseNextAck(); await f.manager().created(f.item());
  assert.equal(f.data['download-outbox'].pending.length, 1);
  await f.manager().reconcile();
  assert.equal(f.requests.length, 2); assert.equal(f.delivered.length, 1);
  assert.equal(f.requests[0].body.sequence, f.requests[1].body.sequence);
  assert.equal(f.data['download-outbox'].pending.length, 0);
});
test('worker rehydration retains offline Created and terminal events in order', async () => {
  const f = fixture(); f.setOffline(true); const m = f.manager(); await m.created(f.item());
  f.items.set(1, f.item(1, 'complete')); await m.changed({ id: 1, state: { current: 'complete' } });
  assert.equal(f.data['download-outbox'].pending.length, 2);
  f.setOffline(false); await f.manager().reconcile();
  assert.deepEqual(f.delivered.map(e => e.event.state), ['Created', 'Complete']);
  assert.equal(f.data['download-outbox'].pending.length, 0);
});
test('failed official lookup cannot lose a persisted terminal transition', async () => {
  const f = fixture(), m = f.manager(); await m.created(f.item()); f.setLookupFails(true);
  await m.changed({ id: 1, state: { current: 'interrupted' }, error: { current: 'SERVER_FAILED' } });
  assert.equal(f.data['download-outbox'].pending[0].resolve, true);
  f.setLookupFails(false); f.items.set(1, f.item(1, 'interrupted')); await f.manager().reconcile();
  assert.equal(f.delivered.at(-1).event.error, 'SERVER_FAILED');
});
test('worker recovery catches missed completion only for an already observed ID', async () => {
  const f = fixture(); await f.manager().created(f.item());
  f.items.set(1, f.item(1, 'complete')); f.items.set(999, f.item(999, 'complete'));
  await f.manager().reconcile(); await f.manager().reconcile();
  assert.deepEqual(f.delivered.map(e => e.event.state), ['Created', 'Complete']);
  assert(f.searches.every(id => id === 1));
});
test('unobserved history, unrelated changes and incognito items are ignored', async () => {
  const f = fixture(), m = f.manager();
  await m.changed({ id: 999, state: { current: 'complete' } });
  await m.created({ ...f.item(), incognito: true }); await m.reconcile();
  assert.equal(f.requests.length, 0); assert.equal(f.searches.length, 0);
});
test('no late Interrupted or duplicate Created/Complete after Complete', async () => {
  const f = fixture(), m = f.manager(); f.items.set(1, f.item(1, 'complete'));
  await m.created(f.item(1, 'complete')); await m.created(f.item());
  await m.changed({ id: 1, state: { current: 'complete' } });
  await m.changed({ id: 1, state: { current: 'interrupted' } });
  assert.deepEqual(f.delivered.map(e => e.event.state), ['Created', 'Complete']);
});
test('closed representative rotates to another live capability without attribution', async () => {
  const f = fixture(5); f.setOffline(true); await f.manager().created(f.item());
  const previous = f.requests[0].representative;
  f.records.find(r => r.appSessionId === previous).closed = true;
  f.setOffline(false); await f.manager().reconcile();
  assert.notEqual(f.requests.at(-1).representative, previous); assert.equal(f.delivered.length, 1);
});
test('two independent bridges each receive once even when one delivery fails', async () => {
  const f = fixture(5), second = { ...f.records[0], bridgeId: crypto.randomUUID(), appSessionId: crypto.randomUUID() };
  f.records.push(second); f.offlineDestination(second.bridgeId);
  await f.manager().created(f.item()); assert.equal(f.delivered.length, 1);
  f.offlineDestination(null); await f.manager().reconcile();
  assert.equal(f.delivered.length, 2); assert.equal(new Set(f.delivered.map(e => e.bridgeId)).size, 2);
  assert.equal(f.data['download-outbox'].pending.length, 0);
});
test('new bridges do not get retroactive profile history', async () => {
  const f = fixture(); await f.manager().created(f.item());
  f.records.push({ ...f.records[0], bridgeId: crypto.randomUUID(), appSessionId: crypto.randomUUID() });
  f.items.set(1, f.item(1, 'complete')); await f.manager().reconcile();
  assert.equal(new Set(f.delivered.map(e => e.bridgeId)).size, 1);
});
test('outbox and known-download retention are bounded during outage', async () => {
  const f = fixture(); f.setOffline(true); const m = f.manager();
  for (let id = 1; id <= DOWNLOAD_LIMITS.tracked + 2; id++) {
    await m.created(f.item(id)); f.items.set(id, f.item(id, 'complete'));
    await m.changed({ id, state: { current: 'complete' } });
  }
  const state = f.data['download-outbox'];
  assert.equal(state.tracked.length, DOWNLOAD_LIMITS.tracked);
  assert(state.pending.length <= DOWNLOAD_LIMITS.pending); assert(!state.tracked.some(t => t.id === 1));
  assert(state.pending.every(e => state.tracked.some(t => t.id === e.event.downloadId)));
});
test('bridge fan-out and path sizes are bounded', async () => {
  const f = fixture(20); f.records.forEach(r => { r.bridgeId = crypto.randomUUID(); });
  await f.manager().created(f.item()); assert.equal(f.delivered.length, DOWNLOAD_LIMITS.bridges);
  await f.manager().created({ ...f.item(2), filename: 'x'.repeat(1025) });
  assert.equal(f.delivered.length, DOWNLOAD_LIMITS.bridges);
});
test('full browser-session storage reset does not scan or replay Chrome history', async () => {
  const f = fixture(); f.items.set(1, f.item(1, 'complete'));
  await f.manager().reconcile(); assert.equal(f.searches.length, 0); assert.equal(f.delivered.length, 0);
});
