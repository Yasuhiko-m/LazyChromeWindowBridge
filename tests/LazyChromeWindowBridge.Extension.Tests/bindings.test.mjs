import test from 'node:test';
import assert from 'node:assert/strict';
import { BindingManager } from '../../src/LazyChromeWindowBridge.Extension/bindings.js';

test('default browser transport preserves the native fetch receiver', async () => {
  const original = globalThis.fetch;
  globalThis.fetch = async function () { assert.equal(this, globalThis); return { ok: true }; };
  try {
    await new BindingManager({}).requestSession({ origin: 'http://127.0.0.1:1234', appSessionId: crypto.randomUUID(), token: 'test' }, 'bind');
  } finally { globalThis.fetch = original; }
});

function fixture() {
  const data = {}, windows = new Map([[11, {}], [12, {}]]), tabs = new Map(), reports = [];
  const descriptions = new Map();
  let offline = false;
  const browser = {
    runtime: { id: 'extension-id' },
    storage: { session: {
      async get(key) { return structuredClone(key === null ? data : { [key]: data[key] }); },
      async set(values) { Object.assign(data, structuredClone(values)); },
      async remove(key) { delete data[key]; }
    } },
    windows: { async get(id) { if (!windows.has(id)) throw Error('missing'); return windows.get(id); } },
    tabs: {
      async get(id) { if (!tabs.has(id)) throw Error('missing'); return structuredClone(tabs.get(id)); },
      async update(id, patch) { Object.assign(tabs.get(id), patch); }
    }
  };
  const request = async (url, options) => {
    if (offline) throw Error('offline');
    const session = url.split('/sessions/')[1].split('/')[0];
    if (options.method === 'GET') return { ok: true, json: async () => descriptions.get(session) };
    reports.push({ session, action: url.split('/').at(-1), ...JSON.parse(options.body) });
    return { ok: true };
  };
  function bootstrap(windowId, tabId) {
    const appSessionId = crypto.randomUUID(), bridgeId = crypto.randomUUID();
    const url = `http://127.0.0.1:45678/lazy-chrome-window-bridge/bootstrap#v=1&bridge=${bridgeId}&session=${appSessionId}&token=${'A'.repeat(64)}`;
    descriptions.set(appSessionId, { appSessionId, bridgeId, launchUrl: 'https://example.test/launch' });
    tabs.set(tabId, { windowId, url });
    return { message: { url }, sender: { id: browser.runtime.id, frameId: 0, tab: { id: tabId, windowId }, url }, appSessionId };
  }
  return { data, windows, tabs, reports, descriptions, browser, request, bootstrap, setOffline(value) { offline = value; } };
}

test('same production rehydration path preserves two bindings and arbitrary navigation', async () => {
  const f = fixture();
  const a = f.bootstrap(11, 101), b = f.bootstrap(12, 102);
  await new BindingManager(f.browser, f.request).bootstrap(a.message, a.sender);
  await new BindingManager(f.browser, f.request).bootstrap(b.message, b.sender);
  f.tabs.get(101).url = 'https://unrelated.example/after-navigation';
  const cold = new BindingManager(f.browser, f.request);
  await cold.reconcile();
  const records = await cold.records();
  assert.equal(records.find(r => r.appSessionId === a.appSessionId).windowId, 11);
  assert.equal(records.find(r => r.appSessionId === b.appSessionId).windowId, 12);
  assert.equal(f.tabs.get(101).url, 'https://unrelated.example/after-navigation');
  await assert.rejects(cold.bootstrap(a.message, { ...a.sender, tab: { id: 102, windowId: 12 } }));
});

test('persisted close outbox survives worker restart and offline delivery', async () => {
  const f = fixture(), a = f.bootstrap(11, 101), b = f.bootstrap(12, 102);
  const manager = new BindingManager(f.browser, f.request);
  await manager.bootstrap(a.message, a.sender);
  await manager.bootstrap(b.message, b.sender);
  f.setOffline(true);
  f.windows.delete(11);
  await manager.windowRemoved(11);
  assert.equal((await manager.records()).find(r => r.appSessionId === a.appSessionId).closed, true);
  f.setOffline(false);
  const cold = new BindingManager(f.browser, f.request);
  await cold.reconcile();
  assert.deepEqual((await cold.records()).map(r => r.appSessionId), [b.appSessionId]);
  assert(f.reports.some(r => r.session === a.appSessionId && r.action === 'closed' && r.windowId === 11));
});

test('recovery discovers a close missed while worker was stopped', async () => {
  const f = fixture(), a = f.bootstrap(11, 101);
  await new BindingManager(f.browser, f.request).bootstrap(a.message, a.sender);
  f.windows.delete(11);
  const cold = new BindingManager(f.browser, f.request);
  await cold.reconcile();
  assert.equal((await cold.records()).length, 0);
  assert.equal(f.reports.at(-1).action, 'closed');
});

test('pending bootstrap replay never overwrites navigation that already happened', async () => {
  const f = fixture(), a = f.bootstrap(11, 101);
  const manager = new BindingManager(f.browser, f.request);
  await manager.bootstrap(a.message, a.sender);
  const record = (await manager.records())[0];
  record.navigationPending = true; // Exact persisted state if worker dies after tabs.update, before storage.set.
  await manager.save(record);
  f.tabs.get(101).url = 'https://another.example/keep';
  await new BindingManager(f.browser, f.request).reconcile();
  assert.equal(f.tabs.get(101).url, 'https://another.example/keep');
});

test('only the trusted top-frame local bootstrap sender is accepted', async () => {
  const f = fixture(), a = f.bootstrap(11, 101);
  const manager = new BindingManager(f.browser, f.request);
  for (const sender of [{ ...a.sender, frameId: 1 }, { ...a.sender, id: 'another-extension' }, { ...a.sender, url: 'https://example.test/' }])
    await assert.rejects(manager.bootstrap(a.message, sender));
  assert.equal((await manager.records()).length, 0);
});

test('native mapping must acknowledge before navigation; cold recovery retries the same window', async () => {
  const f = fixture(), a = f.bootstrap(11, 101);
  f.descriptions.get(a.appSessionId).nativeGeometry = true;
  const url = new URL(a.message.url);
  url.search = `?session=${a.appSessionId}`;
  a.message.url = a.sender.url = url.href;
  f.tabs.get(101).url = url.href;
  let available = false;
  const request = async (url, options) => url.endsWith('/native') && !available ? { ok: false, status: 503 } : f.request(url, options);
  const manager = new BindingManager(f.browser, request);
  await assert.rejects(manager.bootstrap(a.message, a.sender));
  assert.equal(f.tabs.get(101).url, url.href);
  assert.equal((await manager.records())[0].nativePending, true);
  available = true;
  await new BindingManager(f.browser, request).reconcile();
  assert.equal(f.tabs.get(101).url, f.descriptions.get(a.appSessionId).launchUrl);
  assert.equal(f.reports.at(-1).action, 'native');
  assert.equal(f.reports.at(-1).windowId, 11);
  assert.equal((await manager.records())[0].nativePending, false);
});

test('native marker query cannot claim another session or extra parameters', async () => {
  const f = fixture(), a = f.bootstrap(11, 101);
  for (const query of [`?session=${crypto.randomUUID()}`, `?session=${a.appSessionId}&extra=1`]) {
    const url = new URL(a.message.url); url.search = query;
    await assert.rejects(new BindingManager(f.browser, f.request).bootstrap({ url: url.href }, { ...a.sender, url: url.href }));
  }
  assert.equal(Object.keys(f.data).length, 0);
});
