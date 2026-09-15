// Source-owned real Windows/CfT acceptance for exact-HWND capture and taskbar policy.
import assert from 'node:assert/strict';
import { observeMonitor } from './MonitorContinuity.mjs';

export async function testNativeWindow({ caller, cdp, until, delay, evidence, pageA }) {
  const audit = await observeMonitor(cdp);
  const topology = await caller('monitors');
  const work = topology.find(m => m.primary).workArea;
  const outside = r => topology.every(m => r.left + r.width <= m.bounds.left || r.left >= m.bounds.left + m.bounds.width || r.top + r.height <= m.bounds.top || r.top >= m.bounds.top + m.bounds.height);
  const rows = [];
  for (let i = 0; i < 5; i++) {
    const url = `${pageA.url}/dynamic-${'abcde'[i]}`;
    const made = await caller('launch', url);
    const geometry = await until(() => caller('geometry', { id: made.appSessionId }), g => g?.state === 'Visible', `NativeWindow map ${i}`, 45000);
    const session = (await caller('sessions')).find(s => s.appSessionId === made.appSessionId);
    await until(() => cdp.extension(`chrome.tabs.get(${session.tabId}).then(t=>({status:t.status,url:t.url}))`), t => t.status === 'complete' && t.url === url, `NativeWindow fixture ${i}`);
    const normal = { left: work.left + 35 + i * 28, top: work.top + 45 + i * 24, width: 1280, height: 800 };
    await caller('set-bounds', { id: session.appSessionId, rect: normal });
    rows.push({ ...session, identity: geometry.identity, normal });
  }
  const mixedParked = rows.slice(1).filter((_, i) => i % 2 === 0);
  for (const row of mixedParked) {
    const parked = await caller('park', { id: row.appSessionId });
    assert(outside(parked.current));
  }

  // Establish the preserved BrowserViewport path and a physical-size comparison.
  await caller('monitor-start', { options: { framesPerSecond: 2, maxWidth: 1920, maxHeight: 1080, mode: 'BrowserViewport' } });
  // This phase compares one visible window only. The normal BrowserViewport suite
  // separately owns the multi-window Visible/Parked regression coverage.
  for (const row of rows.slice(1)) await caller('monitor-session', { id: row.appSessionId, enabled: false });
  await until(() => caller('monitor'), m => m.sessions.find(s => s.appSessionId === rows[0].appSessionId)?.state === 'Live', 'BrowserViewport baseline for NativeWindow comparison', 45000);
  const browserFrame = await caller('monitor-frame', { id: rows[0].appSessionId });
  assert.equal(browserFrame.mode, 'BrowserViewport'); assert.equal(browserFrame.tabId, rows[0].tabId);
  const browserAudit = await audit();
  assert(browserAudit.tabs[rows[0].tabId].captures > 0);

  await caller('monitor-start', { options: { framesPerSecond: 2, maxWidth: 1920, maxHeight: 1080, mode: 'NativeWindow' } });
  for (const row of rows.slice(1)) await caller('monitor-session', { id: row.appSessionId, enabled: true });
  await until(() => caller('monitor'), m => m.sessions.every(s => s.state === 'Live' && s.mode === 'NativeWindow'), 'five mixed NativeWindow streams', 45000);
  const nativeFrame = await caller('monitor-frame', { id: rows[0].appSessionId });
  const nativeGeometry = await caller('geometry', { id: rows[0].appSessionId });
  assert.equal(nativeFrame.mode, 'NativeWindow'); assert.equal(nativeFrame.tabId, -1);
  assert.deepEqual(nativeFrame.identity, nativeGeometry.identity); assert.equal(nativeFrame.windowId, rows[0].windowId);
  assert(nativeFrame.height > browserFrame.height + 30, `expected native non-client extent: ${JSON.stringify({ browserFrame, nativeFrame })}`);
  assert(Math.abs(nativeFrame.width - nativeGeometry.current.width) <= 32 && Math.abs(nativeFrame.height - nativeGeometry.current.height) <= 32);
  const nativeAudit = await audit();
  await delay(1200);
  const laterAudit = await audit();
  for (const row of rows) {
    assert.equal(laterAudit.tabs[row.tabId].captures, nativeAudit.tabs[row.tabId].captures, 'NativeWindow must not request CDP screenshots');
  }
  evidence('native-window-exact-composition-distinction', { result: 'PASS', browserFrame, nativeFrame, nativeGeometry,
    nativeMinusViewportHeight: nativeFrame.height - browserFrame.height, pickerUsed: false, cdpCapturesDuringNative: 0 });

  const stats = async () => caller('process-stats', { pids: (await cdp.call('SystemInfo.getProcessInfo')).processInfo.map(p => p.id) });
  async function sample(label, targets, seconds = 6) {
    await until(() => caller('monitor'), m => targets.every(t => m.sessions.find(s => s.appSessionId === t.appSessionId)?.state === 'Live'), label + ' live', 45000);
    const before = await caller('monitor'), beforeStats = await stats(), started = performance.now();
    const seen = new Map(targets.map(t => [t.appSessionId, new Map()]));
    while (performance.now() - started < seconds * 1000) {
      for (const frame of await caller('monitor-frames'))
        if (seen.has(frame.appSessionId)) seen.get(frame.appSessionId).set(frame.sequence, frame);
      await delay(100);
    }
    const elapsed = (performance.now() - started) / 1000, after = await caller('monitor'), afterStats = await stats();
    const placements = new Map(await Promise.all(targets.map(async target => {
      const geometry = await caller('geometry', { id: target.appSessionId });
      return [target.appSessionId, geometry.state];
    })));
    const perSession = targets.map(target => {
      const frames = [...seen.get(target.appSessionId).values()];
      const current = after.sessions.find(s => s.appSessionId === target.appSessionId), previous = before.sessions.find(s => s.appSessionId === target.appSessionId);
      // FPS is a ceiling, not a throughput SLA. Two distinct bounded frames prove
      // continued freshness even when offscreen WGC composition is throttled.
      assert(frames.length >= 2);
      assert(frames.at(-1).sequence > frames[0].sequence && frames.at(-1).receivedAt !== frames[0].receivedAt);
      for (const frame of frames) {
        assert.equal(frame.mode, 'NativeWindow'); assert.equal(frame.tabId, -1); assert.equal(frame.windowId, target.windowId);
        assert.deepEqual(frame.identity, (after.sessions.find(s => s.appSessionId === target.appSessionId)).identity);
        assert(frame.width <= 240 && frame.height <= 135);
      }
      return { appSessionId: target.appSessionId, windowId: target.windowId, placement: placements.get(target.appSessionId),
        frames: current.frames - previous.frames, effectiveFps: (current.frames - previous.frames) / elapsed,
        jpegBytes: frames.reduce((sum, frame) => sum + frame.bytes, 0),
        meanCaptureMilliseconds: frames.reduce((sum, frame) => sum + frame.captureMilliseconds, 0) / frames.length,
        dimensions: [frames.at(-1).width, frames.at(-1).height] };
    });
    const metrics = { elapsedSeconds: elapsed, aggregateFps: (after.frames - before.frames) / elapsed,
      aggregateJpegBytesPerSecond: (after.bytes - before.bytes) / elapsed,
      callerOneCoreCpuPercent: (afterStats.callerCpuSeconds - beforeStats.callerCpuSeconds) / elapsed * 100,
      chromeOneCoreCpuPercent: (afterStats.chromeCpuSeconds - beforeStats.chromeCpuSeconds) / elapsed * 100,
      callerWorkingMiB: afterStats.callerWorkingBytes / 1048576, callerPrivateMiB: afterStats.callerPrivateBytes / 1048576,
      chromeWorkingMiB: afterStats.chromeWorkingBytes / 1048576, chromePrivateMiB: afterStats.chromePrivateBytes / 1048576, perSession };
    evidence(label, { result: 'PASS', metrics });
    return metrics;
  }

  // Reopen acquisitions at default bounds. Global restart clears the deliberately
  // large composition-comparison frames and makes every measured frame bounded.
  for (const row of mixedParked) await caller('restore', { id: row.appSessionId });
  await caller('monitor-stop');
  await until(() => caller('monitor'), m => m.capturingConnections === 0, 'comparison acquisitions stopped');
  await caller('monitor-start', { options: { framesPerSecond: 2, maxWidth: 240, maxHeight: 135, mode: 'NativeWindow' } });
  await until(() => caller('monitor'), m => m.sessions.every(s => s.state === 'Live' && s.mode === 'NativeWindow'), 'default NativeWindow restart', 45000);
  // Use the last-launched visible window for the one-session measurement so it is
  // not fully occluded beneath the other four overlapping Chrome windows.
  const oneTarget = rows.at(-1);
  for (const row of rows.filter(row => row !== oneTarget)) await caller('monitor-session', { id: row.appSessionId, enabled: false });
  await until(() => caller('monitor'), m => m.capturingConnections === 1, 'one NativeWindow resource');
  await sample('native-window-one-session-default', [oneTarget]);
  const peerRestart = await caller('monitor');
  for (const row of rows.filter(row => row !== oneTarget)) await caller('monitor-session', { id: row.appSessionId, enabled: true });
  await until(() => caller('monitor'), m => m.capturingConnections === 5 && rows.every(row =>
    m.sessions.find(s => s.appSessionId === row.appSessionId).frames >
    peerRestart.sessions.find(s => s.appSessionId === row.appSessionId).frames), 'five visible NativeWindow resources fresh', 45000);
  // Keep each acquisition generation alive across PARK so this validates the
  // accepted placement/monitor independence rather than an offscreen cold start.
  for (const row of mixedParked) await caller('park', { id: row.appSessionId });
  await sample('native-window-five-session-mixed-default', rows);

  // The mixed-placement sample above owns parked freshness evidence. Restore its
  // parked peers before the independent pause test so Chrome keeps every peer's
  // compositor active while we assert that the paused target alone freezes.
  for (const row of rows) {
    if ((await caller('geometry', { id: row.appSessionId })).state === 'Parked')
      await caller('restore', { id: row.appSessionId });
  }
  const target = rows[0], peers = rows.slice(1);
  await caller('monitor-session', { id: target.appSessionId, enabled: false });
  const frozen = await caller('monitor-frame', { id: target.appSessionId });
  const peersBefore = await caller('monitor');
  await caller('park', { id: target.appSessionId });
  await caller('restore', { id: target.appSessionId });
  const paused = await until(() => caller('monitor'), current => peers.every(peer =>
    current.sessions.find(s => s.appSessionId === peer.appSessionId).frames >
    peersBefore.sessions.find(s => s.appSessionId === peer.appSessionId).frames),
  'NativeWindow peers continue while target paused', 10000);
  assert.equal(paused.sessions.find(s => s.appSessionId === target.appSessionId).state, 'Paused');
  assert.deepEqual(await caller('monitor-frame', { id: target.appSessionId }), frozen);
  for (const peer of peers) assert(paused.sessions.find(s => s.appSessionId === peer.appSessionId).frames > peersBefore.sessions.find(s => s.appSessionId === peer.appSessionId).frames);
  await caller('monitor-session', { id: target.appSessionId, enabled: true });
  await until(() => caller('monitor-frame', { id: target.appSessionId }), f => f?.sequence > frozen.sequence, 'NativeWindow resume freshness');
  evidence('native-window-pause-park-restore-resume', { result: 'PASS', frozen, resumed: await caller('monitor-frame', { id: target.appSessionId }), peers: (await caller('monitor')).sessions.slice(1) });

  const taskbarBeforeA = await caller('taskbar-evidence', { id: rows[0].appSessionId });
  const taskbarBeforeB = await caller('taskbar-evidence', { id: rows[1].appSessionId });
  const hidden = await caller('taskbar', { id: rows[0].appSessionId, show: false });
  assert(hidden.hidden); assert.equal(hidden.identity.hwnd, taskbarBeforeA.identity.hwnd);
  assert((hidden.currentStyle & 0x80) !== 0 && (hidden.currentStyle & 0x40000) === 0);
  assert.deepEqual(await caller('taskbar-evidence', { id: rows[1].appSessionId }), taskbarBeforeB);
  await caller('park', { id: rows[0].appSessionId }); await caller('restore', { id: rows[0].appSessionId });
  assert.deepEqual(await caller('taskbar-evidence', { id: rows[0].appSessionId }), hidden);
  assert.equal((await caller('monitor')).sessions.find(s => s.appSessionId === rows[0].appSessionId).state, 'Live');
  const shown = await caller('taskbar', { id: rows[0].appSessionId, show: true });
  assert(!shown.hidden); assert.equal(shown.currentStyle, taskbarBeforeA.originalStyle);
  await caller('taskbar', { id: rows[0].appSessionId, show: false });
  evidence('taskbar-exact-hwnd-isolation', { result: 'PASS', before: taskbarBeforeA, peer: taskbarBeforeB, hidden, shown,
    shellBoundary: 'Extended-style state and successful native calls are deterministic; final Explorer button pixels require separate visual GUI observation and are not claimed here.' });

  await caller('monitor-stop');
  await until(() => caller('monitor'), m => m.capturingConnections === 0, 'NativeWindow global Stop zero active resources');
  for (const row of rows) assert.equal(await caller('monitor-frame', { id: row.appSessionId }), null);
  await caller('monitor-start', { options: { framesPerSecond: 2, maxWidth: 240, maxHeight: 135, mode: 'NativeWindow' } });
  await until(() => caller('monitor'), m => m.sessions.every(s => s.state === 'Live'), 'NativeWindow global Start batch restart', 45000);
  for (const row of rows.slice(1)) if ((await caller('geometry', { id: row.appSessionId })).state === 'Visible') await caller('park', { id: row.appSessionId });
  const shutdown = await caller('shutdown-host');
  assert.equal(shutdown.connections, 0); assert.equal(shutdown.capturingConnections, 0);
  const taskbarRestoration = await caller('shutdown-taskbar');
  assert(taskbarRestoration.some(r => r.identity.hwnd === taskbarBeforeA.identity.hwnd && r.restored && r.originalStyle === r.restoredStyle));
  const finalGeometry = await caller('shutdown-geometry');
  assert(finalGeometry.filter(g => rows.some(r => r.appSessionId === g.appSessionId)).every(g => !outside(g.current)));
  evidence('native-window-stop-start-shutdown', { result: 'PASS', shutdown, taskbarRestoration, finalGeometry });
}
