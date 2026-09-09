import test from 'node:test';
import assert from 'node:assert/strict';
import { MonitorManager } from '../src/Extension/monitor.js';

const pause = ms => new Promise(resolve => setTimeout(resolve, ms));
async function until(read, timeout = 2500) { const end = Date.now() + timeout; while (!read()) { if (Date.now() > end) throw Error('monitor unit timeout'); await pause(10); } }
function fixture() {
  const data = {}, attached = new Set(), calls = [], sockets = [];
  let detachEvent, fail = false, screenshotWait = null;
  const tabs = new Map([[101, { id: 101, windowId: 11, active: true }], [102, { id: 102, windowId: 12, active: true }]]);
  const browser = {
    storage: { session: { async get() { return structuredClone(data); }, async set(value) { Object.assign(data, value); }, async remove(key) { delete data[key]; } } },
    tabs: { async query(query) { return [...tabs.values()].filter(t => t.windowId === query.windowId && t.active); }, async get(id) { if (!tabs.has(id)) throw Error('closed'); return structuredClone(tabs.get(id)); } },
    debugger: {
      onDetach: { addListener(handler) { detachEvent = handler; } },
      async attach(target) { calls.push(['attach', target.tabId]); attached.add(target.tabId); },
      async detach(target) { calls.push(['detach', target.tabId]); attached.delete(target.tabId); },
      async sendCommand(target, method) {
        calls.push([method, target.tabId]);
        if (fail) throw Error('capture unavailable');
        if (method === 'Page.getLayoutMetrics') return { cssVisualViewport: { pageX: 0, pageY: 0, clientWidth: 1000, clientHeight: 600 } };
        if (screenshotWait) await screenshotWait;
        return { data: 'fixture-jpeg-bytes' };
      }
    }
  };
  class Socket {
    readyState = 1; bufferedAmount = 0; sent = [];
    constructor(url) { this.url = url; sockets.push(this); queueMicrotask(() => this.onopen?.()); }
    send(message) { this.sent.push(JSON.parse(message)); }
    close() { if (this.readyState !== 1) return; this.readyState = 3; this.onclose?.(); }
    control(generation, enabled = true, closing = false) { this.onmessage({ data: JSON.stringify({ generation, enabled, closing, options: { framesPerSecond: 10, maxWidth: 960, maxHeight: 540 } }) }); }
  }
  const manager = new MonitorManager(browser, Socket);
  const record = { appSessionId: 'owned-a', windowId: 11, token: 'capability', origin: 'http://127.0.0.1:12345', monitoring: true, closed: false };
  return { manager, record, sockets, calls, attached, data, tabs,
    fail(value) { fail = value; }, waitScreenshot(value) { screenshotWait = value; }, cancel() { attached.delete(101); detachEvent({ tabId: 101 }, 'canceled_by_user'); } };
}

test('monitor targets the owned window active tab, uses only pixel/viewport commands and stops idempotently', async () => {
  const f = fixture(); f.manager.ensure(f.record); f.manager.ensure(f.record);
  assert.equal(f.sockets.length, 1);
  const socket = f.sockets[0]; socket.control(1);
  await until(() => socket.sent.filter(m => m.type === 'frame').length >= 2);
  assert.deepEqual([...new Set(f.calls.filter(c => c[0].startsWith('Page.')).map(c => c[0]))].sort(), ['Page.captureScreenshot', 'Page.getLayoutMetrics']);
  assert(socket.sent.filter(m => m.type === 'frame').every(m => m.windowId === 11 && m.tabId === 101 && m.generation === 1));
  socket.control(2, false); socket.control(2, false);
  await until(() => f.attached.size === 0);
  f.manager.remove(f.record.appSessionId); f.manager.remove(f.record.appSessionId);
  await until(() => f.manager.connections.size === 0);
});

test('a tab moved out of the owned window during acquisition cannot publish its frame', async () => {
  const f = fixture(); let release;
  f.waitScreenshot(new Promise(resolve => { release = resolve; }));
  f.manager.ensure(f.record); const socket = f.sockets[0]; socket.control(1);
  await until(() => f.calls.some(c => c[0] === 'Page.captureScreenshot'));
  f.tabs.get(101).windowId = 12; release();
  await until(() => socket.sent.some(m => m.error));
  assert.equal(socket.sent.filter(m => m.type === 'frame').length, 0);
  await until(() => f.attached.size === 0); socket.close();
});

test('user cancellation is respected until a new explicit monitor generation', async () => {
  const f = fixture(); let release;
  f.waitScreenshot(new Promise(resolve => { release = resolve; }));
  f.manager.ensure(f.record); const socket = f.sockets[0]; socket.control(1);
  await until(() => f.calls.some(c => c[0] === 'Page.captureScreenshot'));
  f.cancel(); release(); await pause(150);
  assert.equal(socket.sent.filter(m => m.type === 'frame').length, 0);
  const before = f.calls.filter(c => c[0] === 'attach').length;
  socket.control(1); await pause(150);
  assert.equal(f.calls.filter(c => c[0] === 'attach').length, before);
  assert(socket.sent.some(m => m.error?.includes('canceled_by_user')));
  socket.control(2); await until(() => f.attached.has(101));
  socket.control(3, false, true); await until(() => socket.readyState === 3 && f.attached.size === 0);
});

test('capture failure detaches resources and does not loop through permission retries', async () => {
  const f = fixture(); f.fail(true); f.manager.ensure(f.record); const socket = f.sockets[0]; socket.control(1);
  await until(() => socket.sent.some(m => m.error));
  await until(() => f.attached.size === 0);
  const count = f.calls.filter(c => c[0] === 'attach').length;
  socket.control(1); await pause(150);
  assert.equal(f.calls.filter(c => c[0] === 'attach').length, count);
  assert.equal(f.record.windowId, 11); assert.equal(f.record.closed, false); socket.close();
});

test('worker recovery cleans only persisted owned debugger targets', async () => {
  const f = fixture(); f.data['monitor-target:owned-a'] = { tabId: 101 }; f.data['binding:owned-a'] = f.record;
  f.attached.add(101); await f.manager.recover();
  assert.equal(f.attached.size, 0); assert.deepEqual(f.calls, [['detach', 101]]);
  assert(f.data['binding:owned-a']); assert(!f.data['monitor-target:owned-a']);
});

test('closing the caller transport discards in-flight pixels and detaches', async () => {
  const f = fixture(); let release;
  f.waitScreenshot(new Promise(resolve => { release = resolve; }));
  f.manager.ensure(f.record); const socket = f.sockets[0]; socket.control(1);
  await until(() => f.calls.some(c => c[0] === 'Page.captureScreenshot'));
  socket.close(); release();
  await until(() => f.attached.size === 0);
  assert.equal(socket.sent.filter(m => m.type === 'frame').length, 0);
});
