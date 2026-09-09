// Source-owned isolated consumer acceptance. No page DOM access; local attachment navigation only.
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import path from 'node:path';
import http from 'node:http';

// A valid ZIP with a neutral, uncompressed text entry; no third-party fixture/dependency.
function zip(size) {
  const name = Buffer.from('neutral.txt'), data = Buffer.alloc(size, 0x61);
  let crc = 0xffffffff;
  for (const byte of data) { crc ^= byte; for (let i = 0; i < 8; i++) crc = (crc >>> 1) ^ (crc & 1 ? 0xedb88320 : 0); }
  crc = (crc ^ 0xffffffff) >>> 0;
  const local = Buffer.alloc(30), central = Buffer.alloc(46), end = Buffer.alloc(22);
  local.writeUInt32LE(0x04034b50); local.writeUInt16LE(20, 4); local.writeUInt32LE(crc, 14);
  local.writeUInt32LE(size, 18); local.writeUInt32LE(size, 22); local.writeUInt16LE(name.length, 26);
  central.writeUInt32LE(0x02014b50); central.writeUInt16LE(20, 4); central.writeUInt16LE(20, 6);
  central.writeUInt32LE(crc, 16); central.writeUInt32LE(size, 20); central.writeUInt32LE(size, 24); central.writeUInt16LE(name.length, 28);
  end.writeUInt32LE(0x06054b50); end.writeUInt16LE(1, 8); end.writeUInt16LE(1, 10);
  end.writeUInt32LE(central.length + name.length, 12); end.writeUInt32LE(local.length + name.length + size, 16);
  return Buffer.concat([local, name, data, central, name, end]);
}

export async function testDownloads({ caller, cdp, until, delay, evidence, pageA, profile }) {
  const directory = path.join(profile, 'Downloads');
  await fs.mkdir(directory);
  // Test harness browser configuration only; never a production download API.
  await cdp.call('Browser.setDownloadBehavior', { behavior: 'allow', downloadPath: directory, eventsEnabled: true });
  const files = { normal: zip(256 * 1024), slow: zip(2 * 1024 * 1024), interrupted: zip(4 * 1024 * 1024) };
  const timers = new Set(); let interruptedRequests = 0;
  const server = http.createServer((request, response) => {
    const kind = request.url.slice(1), bytes = files[kind];
    if (!bytes) { response.writeHead(404); response.end(); return; }
    if (kind === 'interrupted') interruptedRequests++;
    response.writeHead(200, { 'Content-Type': 'application/zip', 'Content-Length': bytes.length,
      'Content-Disposition': `attachment; filename="neutral-${kind}.zip"`, 'Cache-Control': 'no-store', 'Accept-Ranges': 'none' });
    let offset = 0;
    const timer = setInterval(() => {
      const next = Math.min(offset + 16384, bytes.length);
      response.write(bytes.subarray(offset, next)); offset = next;
      if (kind === 'interrupted' && offset >= 131072) { response.destroy(); clearInterval(timer); timers.delete(timer); }
      else if (offset === bytes.length) { response.end(); clearInterval(timer); timers.delete(timer); }
    }, kind === 'slow' ? 350 : 100);
    timers.add(timer);
    response.on('close', () => { clearInterval(timer); timers.delete(timer); });
  });
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
  const url = `http://127.0.0.1:${server.address().port}`;
  const events = () => caller('download-events');
  const official = id => cdp.extension(`chrome.downloads.search({id:${id}}).then(a=>a.map(d=>({id:d.id,state:d.state,filename:d.filename,error:d.error??null,exists:d.exists}))[0])`);
  const stream = async id => (await events()).filter(e => e.downloadId === id);
  try {
    for (let i = 0; i < 5; i++) await caller('launch', pageA.url + '/download-session-' + i);
    const sessions = await until(() => caller('sessions'), list => list.length === 5 && list.every(s => s.state === 'Bound'), 'five download sessions bind');
    await until(() => cdp.extension('chrome.storage.session.get(null).then(d=>Object.entries(d).filter(([k])=>k.startsWith("binding:")).map(([,r])=>({ready:!!r.downloads&&!r.navigationPending&&!r.nativePending})))'),
      list => list.length === 5 && list.every(r => r.ready), 'five download capabilities ready');
    const start = async kind => {
      const known = new Set((await events()).map(e => e.downloadId));
      await cdp.extension(`chrome.tabs.update(${sessions[0].tabId},{url:${JSON.stringify(url + '/' + kind)}})`);
      const created = (await until(events, list => list.some(e => !known.has(e.downloadId) && e.state === 'Created'), kind + ' Created')).find(e => !known.has(e.downloadId) && e.state === 'Created');
      assert.deepEqual(Object.keys(created).sort(), ['downloadId', 'state', 'filename', 'error', 'observedAt'].sort());
      return created;
    };
    const complete = async created => {
      const result = await until(() => stream(created.downloadId), list => list.some(e => e.state === 'Complete'), 'Complete', 90000);
      assert.deepEqual(result.map(e => e.state), ['Created', 'Complete']);
      const current = await official(created.downloadId);
      assert.equal(current.state, 'complete'); assert.equal(result[1].filename, current.filename);
      assert.equal(current.id, created.downloadId);
      return { events: result, official: current };
    };
    const normal = await start('normal');
    let filenameBeforeComplete = false;
    await until(async () => {
      const item = await official(normal.downloadId);
      if (item.state === 'in_progress' && item.filename === path.join(directory, 'neutral-normal.zip')) filenameBeforeComplete = true;
      return item.state;
    }, state => state === 'complete', 'normal official completion');
    const normalResult = await complete(normal);
    assert.equal(normalResult.official.filename, path.join(directory, 'neutral-normal.zip'));
    evidence('download-normal-five-sessions', { result: 'PASS', boundSessions: sessions.length, filenameBeforeComplete, ...normalResult });
    const consumed = await caller('consume-download', { id: normal.downloadId, directory, filename: 'neutral-normal.zip' });
    assert.equal(consumed.moved, true); assert.equal(consumed.stable, true); assert.equal(consumed.exclusiveOpen, true);
    await delay(1500);
    const afterMove = await official(normal.downloadId);
    assert.equal(afterMove.state, 'complete'); assert.deepEqual((await stream(normal.downloadId)).map(e => e.state), ['Created', 'Complete']);
    evidence('download-consumer-after-complete', { result: 'PASS', ...consumed, officialAfterMove: afterMove, eventCount: 2 });

    const interrupted = await start('interrupted');
    const brokenStream = await until(() => stream(interrupted.downloadId), list => list.some(e => e.state === 'Interrupted'), 'official Interrupted', 90000);
    const broken = await official(interrupted.downloadId);
    assert.deepEqual(brokenStream.map(e => e.state), ['Created', 'Interrupted']);
    assert.equal(broken.state, 'interrupted'); assert.equal(brokenStream[1].filename, broken.filename);
    assert.equal(brokenStream[1].error, broken.error); assert(broken.error);
    evidence('download-interrupted', { result: 'PASS', serverAbortedRequests: interruptedRequests, events: brokenStream, official: broken });

    const slow = await start('slow'), beforeRestart = await official(slow.downloadId);
    assert.equal(beforeRestart.state, 'in_progress');
    const oldWorker = await cdp.worker();
    const page = (await cdp.call('Target.getTargets')).targetInfos.find(t => t.type === 'page' && t.url === 'about:blank');
    const { sessionId } = await cdp.call('Target.attachToTarget', { targetId: page.targetId, flatten: true });
    await cdp.call('ServiceWorker.enable', {}, sessionId);
    await cdp.call('ServiceWorker.stopAllWorkers', {}, sessionId);
    await until(async () => (await cdp.call('Target.getTargets')).targetInfos.some(t => t.targetId === oldWorker.targetId), value => !value, 'download worker stopped');
    await cdp.call('Target.detachFromTarget', { sessionId });
    const restarted = await cdp.worker();
    assert.notEqual(restarted.targetId, oldWorker.targetId);
    const slowResult = await complete(slow);
    await delay(1500);
    assert.equal((await stream(slow.downloadId)).length, 2);
    assert.deepEqual((await stream(normal.downloadId)).map(e => e.state), ['Created', 'Complete']);
    assert.equal((await caller('sessions')).filter(s => s.state === 'Bound').length, 5);
    evidence('download-mv3-restart', { result: 'PASS', stateWhenStopped: beforeRestart.state, oldTargetId: oldWorker.targetId, newTargetId: restarted.targetId, ...slowResult });
    await until(() => cdp.extension('chrome.storage.session.get("download-outbox").then(d=>d["download-outbox"].pending.length)'), n => n === 0, 'download outbox acknowledgement drain');
    assert.equal((await events()).length, 6);
    await caller('shutdown-host'); assert.deepEqual(await caller('downloads'), []);
    evidence('download-shutdown', { result: 'PASS', consumerEvents: 6, snapshotsAfterDispose: 0, pendingBeforeDispose: 0 });
    console.log('PASS: real normal/interrupted downloads, five-session single stream, MV3 restart and consumer move after Complete.');
  } finally {
    for (const timer of timers) clearInterval(timer);
    server.closeAllConnections(); await new Promise(resolve => server.close(resolve));
  }
}
