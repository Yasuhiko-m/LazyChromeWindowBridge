import test from 'node:test';
import assert from 'node:assert/strict';
import { MonitorManager, resolveCaptureTransform } from '../../src/LazyChromeWindowBridge.Extension/monitor.js';

const pause = ms => new Promise(resolve => setTimeout(resolve, ms));
async function until(read, timeout = 2500) { const end = Date.now() + timeout; while (!read()) { if (Date.now() > end) throw Error('monitor unit timeout'); await pause(10); } }
const options = (region = undefined, resize = null, maxWidth = 240, maxHeight = 135) => ({ framesPerSecond: 2, maxWidth, maxHeight, mode: 0, ...(region ? { region } : {}), ...(resize !== null ? { resize } : {}) });
function fixture() {
  const data = {}, attached = new Set(), calls = [], sockets = [];
  let detachEvent, fail = false, screenshotWait = null;
  const viewport = { pageX: 0, pageY: 0, clientWidth: 1000, clientHeight: 600 };
  const tabs = new Map([[101, { id: 101, windowId: 11, active: true }], [102, { id: 102, windowId: 12, active: true }]]);
  const browser = {
    storage: { session: { async get() { return structuredClone(data); }, async set(value) { Object.assign(data, value); }, async remove(key) { delete data[key]; } } },
    tabs: { async query(query) { return [...tabs.values()].filter(t => t.windowId === query.windowId && t.active); }, async get(id) { if (!tabs.has(id)) throw Error('closed'); return structuredClone(tabs.get(id)); } },
    debugger: {
      onDetach: { addListener(handler) { detachEvent = handler; } },
      async attach(target) { calls.push(['attach', target.tabId]); attached.add(target.tabId); },
      async detach(target) { calls.push(['detach', target.tabId]); attached.delete(target.tabId); },
      async sendCommand(target, method, params) {
        calls.push([method, target.tabId, params]);
        if (fail) throw Error('capture unavailable');
        if (method === 'Page.getLayoutMetrics') return { cssVisualViewport: { ...viewport } };
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
    control(generation, enabled = true, closing = false, options = { framesPerSecond: 10, maxWidth: 960, maxHeight: 540 }) {
      this.onmessage({ data: JSON.stringify({ generation, enabled, closing, options: { mode: 0, ...options } }) });
    }
  }
  const processed = [];
  const manager = new MonitorManager(browser, Socket, async (image, width, height, options) => {
    processed.push({ image, width, height, options: structuredClone(options) }); return { data: 'processed-jpeg-bytes' };
  });
  const record = { appSessionId: 'owned-a', windowId: 11, token: 'capability', origin: 'http://127.0.0.1:12345', monitoring: true, closed: false };
  return { manager, record, sockets, calls, attached, data, tabs, viewport, processed,
    fail(value) { fail = value; }, waitScreenshot(value) { screenshotWait = value; },
    closeTab(id) { tabs.delete(id); attached.delete(id); detachEvent({ tabId: id }, 'target_closed'); },
    cancel() { attached.delete(101); detachEvent({ tabId: 101 }, 'canceled_by_user'); } };
}

test('production capture transform uses floor-partitioned Full, halves, quadrants and spans', () => {
  assert.deepEqual(resolveCaptureTransform(101, 99, options()), { x: 0, y: 0, cropWidth: 101, cropHeight: 99, width: 101, height: 99, smoothingEnabled: true, smoothingQuality: 'medium' });
  assert.deepEqual(resolveCaptureTransform(101, 99, options({ columns: 1, rows: 2, column: 0, row: 1, columnSpan: 1, rowSpan: 1 })), { x: 0, y: 49, cropWidth: 101, cropHeight: 50, width: 101, height: 50, smoothingEnabled: true, smoothingQuality: 'medium' });
  assert.deepEqual(resolveCaptureTransform(101, 99, options({ columns: 2, rows: 2, column: 1, row: 1, columnSpan: 1, rowSpan: 1 })), { x: 50, y: 49, cropWidth: 51, cropHeight: 50, width: 51, height: 50, smoothingEnabled: true, smoothingQuality: 'medium' });
  assert.deepEqual(resolveCaptureTransform(101, 99, options({ columns: 3, rows: 3, column: 0, row: 1, columnSpan: 3, rowSpan: 2 })), { x: 0, y: 33, cropWidth: 101, cropHeight: 66, width: 101, height: 66, smoothingEnabled: true, smoothingQuality: 'medium' });
  const cells = [0, 1, 2].map(column => resolveCaptureTransform(101, 99, options({ columns: 3, rows: 1, column, row: 0, columnSpan: 1, rowSpan: 1 })));
  assert.deepEqual(cells.map(cell => [cell.x, cell.x + cell.cropWidth]), [[0, 33], [33, 67], [67, 101]]);
});

test('production capture transform applies explicit resize, upscale, legacy bounds, filters and safety limits', () => {
  assert.deepEqual(resolveCaptureTransform(120, 80, options(undefined, { width: 80, height: 40, filter: 1 })), { x: 0, y: 0, cropWidth: 120, cropHeight: 80, width: 80, height: 40, smoothingEnabled: true, smoothingQuality: 'medium' });
  assert.deepEqual(resolveCaptureTransform(120, 80, options(undefined, { width: 300, height: null, filter: 0 })), { x: 0, y: 0, cropWidth: 120, cropHeight: 80, width: 300, height: 200, smoothingEnabled: false, smoothingQuality: 'medium' });
  assert.deepEqual(resolveCaptureTransform(120, 80, options(undefined, { width: null, height: 300, filter: 2 })), { x: 0, y: 0, cropWidth: 120, cropHeight: 80, width: 450, height: 300, smoothingEnabled: true, smoothingQuality: 'high' });
  assert.deepEqual(resolveCaptureTransform(1200, 800, options(undefined, null, 240, 135)), { x: 0, y: 0, cropWidth: 1200, cropHeight: 800, width: 202, height: 135, smoothingEnabled: true, smoothingQuality: 'medium' });
  assert.throws(() => resolveCaptureTransform(120, 80, options(undefined, { width: 8192, height: 8192, filter: 1 })), /safe bounds/);
});

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

test('NativeWindow control never attaches debugger or requests viewport screenshot commands', async () => {
  const f = fixture(); f.manager.ensure(f.record); const socket = f.sockets[0];
  socket.control(2, false, false, { framesPerSecond: 2, maxWidth: 240, maxHeight: 135, mode: 1 });
  await pause(100);
  assert.equal(f.calls.length, 0);
  assert.equal(f.attached.size, 0);
  socket.control(3, false, true, { framesPerSecond: 2, maxWidth: 240, maxHeight: 135, mode: 1 });
  await until(() => socket.readyState === 3);
});

test('source-size probe uses layout metrics without a screenshot and detaches its temporary attachment', async () => {
  const f = fixture(); f.manager.ensure(f.record); const socket = f.sockets[0];
  socket.onmessage({ data: JSON.stringify({ generation: 1, enabled: false, closing: false, sourceSizeRequestId: 'a'.repeat(32), options: { framesPerSecond: 2, maxWidth: 240, maxHeight: 135, mode: 0 } }) });
  await until(() => socket.sent.some(m => m.type === 'source-size'));
  assert.deepEqual(socket.sent.find(m => m.type === 'source-size'), { type: 'source-size', requestId: 'a'.repeat(32), width: 1000, height: 600 });
  assert.equal(f.calls.filter(c => c[0] === 'Page.captureScreenshot').length, 0);
  await until(() => f.attached.size === 0); socket.close();
});

test('region and explicit resize options are passed to local processing before final JPEG transport', async () => {
  const f = fixture(); f.manager.ensure(f.record); const socket = f.sockets[0];
  socket.control(1, true, false, { framesPerSecond: 30, maxWidth: 240, maxHeight: 135,
    region: { columns: 3, rows: 3, column: 0, row: 1, columnSpan: 3, rowSpan: 2 }, resize: { width: 300, height: null, filter: 2 } });
  await until(() => f.processed.length > 0 && socket.sent.some(m => m.type === 'frame'));
  assert.deepEqual(f.processed[0].options.region, { columns: 3, rows: 3, column: 0, row: 1, columnSpan: 3, rowSpan: 2 });
  assert.equal(f.processed[0].options.resize.filter, 2);
  assert.equal(socket.sent.find(m => m.type === 'frame').data, 'processed-jpeg-bytes');
  socket.close(); await until(() => f.attached.size === 0);
});

for (const filter of [0, 1, 2]) test(`extension selects requested resize filter ${filter}`, async () => {
  const f = fixture(); f.manager.ensure(f.record); const socket = f.sockets[0];
  socket.control(1, true, false, { framesPerSecond: 30, maxWidth: 240, maxHeight: 135, resize: { width: 80, height: 40, filter } });
  await until(() => f.processed.length > 0);
  assert.equal(f.processed[0].options.resize.filter, filter);
  socket.close(); await until(() => f.attached.size === 0);
});

test('session pause isolates peers and resumes using the same waiting socket', async () => {
  const f = fixture();
  f.manager.ensure(f.record);
  f.manager.ensure({ ...f.record, appSessionId: 'owned-b', windowId: 12 });
  const [a, b] = f.sockets;
  a.control(1); b.control(2);
  const frames = s => s.sent.filter(m => m.type === 'frame').length;
  await until(() => frames(a) >= 2 && frames(b) >= 2);
  const peerAttaches = f.calls.filter(c => c[0] === 'attach' && c[1] === 102).length;
  a.control(3, false);
  await until(() => !f.attached.has(101));
  const stopped = frames(a), peerBefore = frames(b);
  // Placement produces no session enable change. Options still arrive while paused.
  a.control(3, false, false, { framesPerSecond: 30, maxWidth: 240, maxHeight: 135 });
  await until(() => frames(b) > peerBefore + 2);
  assert.equal(frames(a), stopped); assert.equal(a.readyState, 1);
  assert(f.attached.has(102));
  assert.equal(f.calls.filter(c => c[0] === 'detach' && c[1] === 101).length, 1);
  assert.equal(f.calls.filter(c => c[0] === 'detach' && c[1] === 102).length, 0);
  a.control(4);
  await until(() => frames(a) > stopped);
  assert.equal(f.sockets.length, 2); assert.equal(f.sockets[0], a);
  assert.equal(f.calls.filter(c => c[0] === 'attach' && c[1] === 101).length, 2);
  assert.equal(f.calls.filter(c => c[0] === 'attach' && c[1] === 102).length, peerAttaches);
  assert(b.sent.filter(m => m.type === 'frame').every(m => m.generation === 2));
  a.control(5, false, true); b.control(6, false, true);
  await until(() => f.attached.size === 0 && f.manager.connections.size === 0);
});

test('OFF rejects in-flight pixels and rapid ON resumes after old acquisition cleanup', async () => {
  const f = fixture(); let release;
  f.waitScreenshot(new Promise(resolve => { release = resolve; }));
  f.manager.ensure(f.record); const socket = f.sockets[0]; socket.control(1);
  await until(() => f.calls.some(c => c[0] === 'Page.captureScreenshot'));
  socket.control(2, false); socket.control(3);
  f.waitScreenshot(null); release();
  await until(() => socket.sent.some(m => m.type === 'frame' && m.generation === 3));
  assert(socket.sent.filter(m => m.type === 'frame').every(m => m.generation === 3));
  assert.equal(f.sockets.length, 1);
  assert.equal(f.calls.filter(c => c[0] === 'detach').length, 1);
  assert.equal(f.calls.filter(c => c[0] === 'attach').length, 2);
  socket.control(4, false, true); await until(() => f.attached.size === 0 && socket.readyState === 3);
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

test('closing an active tab during capture reacquires only the remaining tab in the owned window', async () => {
  const f = fixture(); let release;
  f.waitScreenshot(new Promise(resolve => { release = resolve; }));
  f.manager.ensure(f.record); const socket = f.sockets[0]; socket.control(1);
  await until(() => f.calls.some(c => c[0] === 'Page.captureScreenshot'));
  f.tabs.set(103, { id: 103, windowId: 11, active: true });
  f.closeTab(101); f.waitScreenshot(null); release();
  await until(() => socket.sent.some(m => m.type === 'frame' && m.tabId === 103));
  assert(socket.sent.filter(m => m.type === 'frame').every(m => m.tabId === 103 && m.windowId === 11));
  assert(!socket.sent.some(m => m.error)); assert(!f.attached.has(102));
  socket.close(); await until(() => f.attached.size === 0);
});

test('worker recovery cleans only persisted owned debugger targets', async () => {
  const f = fixture(); f.data['monitor-target:owned-a'] = { tabId: 101 }; f.data['binding:owned-a'] = f.record;
  f.attached.add(101); await f.manager.recover();
  assert.equal(f.attached.size, 0); assert.deepEqual(f.calls, [['detach', 101]]);
  assert(f.data['binding:owned-a']); assert(!f.data['monitor-target:owned-a']);
});

test('late capture failure belongs to its old generation and cannot poison an explicit retry', async () => {
  const f = fixture(); let rejectCapture;
  f.waitScreenshot(new Promise((_, reject) => { rejectCapture = reject; }));
  f.manager.ensure(f.record); const socket = f.sockets[0]; socket.control(1);
  await until(() => f.calls.some(c => c[0] === 'Page.captureScreenshot'));
  socket.control(2); f.waitScreenshot(null); rejectCapture(Error('old acquisition failed'));
  await until(() => socket.sent.some(m => m.error));
  assert(socket.sent.filter(m => m.error).every(m => m.generation === 1));
  socket.control(2); await until(() => socket.sent.some(m => m.type === 'frame' && m.generation === 2));
  socket.close(); await until(() => f.attached.size === 0);
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

test('five eligible requests share the JPEG path and placement-only control keeps every attachment', async () => {
  const f = fixture();
  for (let i = 1; i <= 5; i++) {
    f.tabs.set(200 + i, { id: 200 + i, windowId: 20 + i, active: true });
    f.manager.ensure({ ...f.record, appSessionId: 'session-' + i, windowId: 20 + i });
    f.sockets[i - 1].control(i);
  }
  await until(() => f.sockets.every(s => s.sent.some(m => m.type === 'frame')));
  assert.equal(f.attached.size, 5);
  f.sockets.forEach((s, index) => assert(s.sent.filter(m => m.type === 'frame').every(m => m.windowId === 21 + index && m.tabId === 201 + index)));
  // Core's placement-only changes produce the same enabled/generation control.
  for (const placement of ['Parked', 'Visible', 'Parked']) {
    const counts = f.sockets.map(s => s.sent.filter(m => m.type === 'frame').length);
    f.sockets[1].control(2);
    await until(() => f.sockets.every((s, i) => s.sent.filter(m => m.type === 'frame').length > counts[i]));
    assert.equal(f.calls.filter(c => c[0] === 'attach').length, 5, placement);
    assert.equal(f.calls.filter(c => c[0] === 'detach').length, 0, placement);
  }
  f.sockets.forEach(s => s.control(30, false, true));
  await until(() => f.attached.size === 0 && f.manager.connections.size === 0);
});

test('same-generation options wake a sleeping pump and change JPEG bounds/rate without detach or reconnect', async () => {
  const f = fixture(); f.manager.ensure(f.record); const socket = f.sockets[0];
  try {
    socket.control(7, true, false, { framesPerSecond: 1, maxWidth: 240, maxHeight: 135 });
    await until(() => socket.sent.some(m => m.type === 'frame'));
    const initial = f.calls.find(c => c[0] === 'Page.captureScreenshot')[2];
    assert.deepEqual(initial, { format: 'png', captureBeyondViewport: false });
    const count = socket.sent.filter(m => m.type === 'frame').length;
    socket.control(7, true, false, { framesPerSecond: 30, maxWidth: 640, maxHeight: 360 });
    await until(() => socket.sent.filter(m => m.type === 'frame').length >= count + 3, 500);
    const changed = f.calls.filter(c => c[0] === 'Page.captureScreenshot').at(-1)[2];
    assert.deepEqual(changed, { format: 'png', captureBeyondViewport: false });
    assert.equal('clip' in changed || 'scale' in changed, false);
    assert.equal(f.processed.at(-1).options.maxWidth, 640);
    assert.equal(f.calls.filter(c => c[0] === 'attach').length, 1);
    assert.equal(f.calls.filter(c => c[0] === 'detach').length, 0);
    assert.equal(f.sockets.length, 1); assert.equal(socket.readyState, 1);
    assert(socket.sent.filter(m => m.type === 'frame').every(m => m.generation === 7));
    f.viewport.clientWidth = 100; f.viewport.clientHeight = 60;
    await until(() => f.processed.some(p => p.width === 100 && p.height === 60));
    assert.deepEqual([...new Set(f.calls.map(c => c[0]))].sort(), ['Page.captureScreenshot', 'Page.getLayoutMetrics', 'attach']);
    socket.control(8, false);
    await until(() => f.attached.size === 0);
    const stopped = socket.sent.filter(m => m.type === 'frame').length;
    await pause(150); assert.equal(socket.sent.filter(m => m.type === 'frame').length, stopped);
    assert.equal(socket.readyState, 1, 'Stop retains the restart-control transport');
    socket.control(9);
    await until(() => socket.sent.some(m => m.type === 'frame' && m.generation === 9));
    assert.equal(f.sockets.length, 1, 'Start resumes the existing socket');
  } finally { socket.close(); await until(() => f.attached.size === 0); }
});

test('options arriving during acquisition apply on the next iteration without losing the old frame or attachment', async () => {
  const f = fixture(); let release;
  f.waitScreenshot(new Promise(resolve => { release = resolve; }));
  f.manager.ensure(f.record); const socket = f.sockets[0]; socket.control(4);
  await until(() => f.calls.some(c => c[0] === 'Page.captureScreenshot'));
  socket.control(4, true, false, { framesPerSecond: 30, maxWidth: 240, maxHeight: 135 });
  f.waitScreenshot(null); release();
  await until(() => socket.sent.filter(m => m.type === 'frame').length >= 2);
  assert.deepEqual(f.calls.filter(c => c[0] === 'Page.captureScreenshot').at(-1)[2], { format: 'png', captureBeyondViewport: false });
  assert.equal(f.calls.filter(c => c[0] === 'detach').length, 0);
  assert.equal(f.calls.filter(c => c[0] === 'attach').length, 1);
  socket.close(); await until(() => f.attached.size === 0);
});

for (const framesPerSecond of [0, 31]) test(`extension rejects ${framesPerSecond} fps`, async () => {
  const f = fixture(); f.manager.ensure(f.record); const socket = f.sockets[0];
  socket.control(1, true, false, { framesPerSecond, maxWidth: 240, maxHeight: 135 });
  await until(() => socket.readyState === 3);
  assert.equal(f.calls.filter(c => c[0] === 'attach').length, 0);
});
