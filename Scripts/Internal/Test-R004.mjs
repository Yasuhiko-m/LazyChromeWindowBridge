import assert from 'node:assert/strict';

export async function testMonitor({ caller, cdp, until, delay, evidence, pageA, pageB }) {
  const near = (a, b) => ['left', 'top', 'width', 'height'].every(k => Math.abs(a[k] - b[k]) <= 2);
  const monitors = await caller('monitors');
  const outside = r => monitors.every(m => r.left + r.width <= m.bounds.left || r.left >= m.bounds.left + m.bounds.width || r.top + r.height <= m.bounds.top || r.top >= m.bounds.top + m.bounds.height);
  const preferred = process.env.LAZY_TEST_WINDOW_POSITION?.split(',').map(Number);
  const work = monitors.find(m => preferred && preferred[0] >= m.bounds.left && preferred[0] < m.bounds.right && preferred[1] >= m.bounds.top && preferred[1] < m.bounds.bottom)?.workArea ?? monitors.find(m => m.primary).workArea;
  const rectA = { left: work.left + 40, top: work.top + 50, width: Math.min(1280, work.width - 100), height: Math.min(800, work.height - 100) };
  const rectB = { ...rectA, left: rectA.left + 80, top: rectA.top + 70, width: rectA.width - 120 };
  const geometry = s => caller('geometry', { id: s.appSessionId });
  async function launch(url) {
    const created = await caller('launch', url);
    await until(() => geometry(created), g => g?.state === 'Visible', 'native capture mapping');
    const s = (await caller('sessions')).find(s => s.appSessionId === created.appSessionId);
    await until(() => cdp.extension('chrome.tabs.get(' + s.tabId + ').then(t=>({url:t.url,status:t.status}))'), t => t.url === url && t.status === 'complete', 'dynamic launch page');
    return s;
  }
  const snapshot = async s => (await caller('monitor')).sessions.find(row => row.appSessionId === s.appSessionId);
  const frame = s => caller('monitor-frame', { id: s.appSessionId });
  async function active(s) {
    await until(() => snapshot(s), row => row.state === 'ACTIVE' && !row.capturing, 'Visible has no capture');
    assert.equal(await frame(s), null);
  }
  async function start(s, options = { framesPerSecond: 2, maxWidth: 960, maxHeight: 540 }) {
    await caller('park', { id: s.appSessionId });
    await caller('monitor-start', { options });
    await until(() => frame(s), f => f?.appSessionId === s.appSessionId, 'parked-session first frame', 45000);
    const started = await snapshot(s);
    await caller('monitor-start', { options });
    assert.equal((await snapshot(s)).generation, started.generation, 'start is idempotent');
    return started;
  }
  async function stats() {
    const processes = (await cdp.call('SystemInfo.getProcessInfo')).processInfo;
    return caller('process-stats', { pids: processes.map(p => p.id) });
  }
  async function sample(label, s, seconds = 5, expectedBlue = false) {
    const begin = await snapshot(s), before = await stats(), t0 = performance.now();
    const samples = new Map();
    while (performance.now() - t0 < seconds * 1000) {
      const frame = await caller('monitor-frame', { id: s.appSessionId });
      if (frame && frame.appSessionId === s.appSessionId) samples.set(frame.sequence, frame);
      await delay(90);
    }
    const elapsed = (performance.now() - t0) / 1000, end = await snapshot(s), after = await stats();
    const frames = [...samples.values()], unique = new Set(frames.map(f => f.centerHash)).size;
    assert(frames.length >= 3 && unique >= 2, label + ' requires multiple fresh content frames: ' + JSON.stringify({ begin, end, sampled: frames.length, unique }));
    const identity = (await geometry(s)).identity;
    for (const f of frames) {
      assert.equal(f.appSessionId, s.appSessionId); assert.equal(f.windowId, s.windowId);
      assert.deepEqual(f.identity, identity);
      const [r, g, b] = f.centerPixel;
      assert(expectedBlue ? b > r + 15 : r > b + 15, 'known neutral fixture center bytes identify A/B: ' + JSON.stringify(f));
    }
    const metrics = { elapsedSeconds: elapsed, receivedFrames: end.frames - begin.frames, sampledFrames: frames.length,
      distinctCenterHashes: unique, effectiveFps: (end.frames - begin.frames) / elapsed,
      base64PayloadBytesPerSecond: (end.bytes - begin.bytes) / elapsed,
      callerOneCoreCpuPercent: (after.callerCpuSeconds - before.callerCpuSeconds) / elapsed * 100,
      chromeOneCoreCpuPercent: (after.chromeCpuSeconds - before.chromeCpuSeconds) / elapsed * 100,
      callerWorkingMiB: after.callerWorkingBytes / 1048576, callerPrivateMiB: after.callerPrivateBytes / 1048576,
      chromeWorkingMiB: after.chromeWorkingBytes / 1048576, chromePrivateMiB: after.chromePrivateBytes / 1048576,
      output: [frames.at(-1).width, frames.at(-1).height],
      maxFrameGapSeconds: Math.max(0, ...frames.slice(1).map((f, i) => (Date.parse(f.receivedAt) - Date.parse(frames[i].receivedAt)) / 1000)),
      meanCaptureMs: frames.reduce((sum, f) => sum + f.captureMilliseconds, 0) / frames.length };
    evidence(label, { result: 'PASS', metrics, first: frames[0], last: frames.at(-1) });
    return metrics;
  }
  async function close(s) {
    await cdp.extension('chrome.windows.remove(' + s.windowId + ')');
    await until(() => caller('sessions'), rows => rows.find(r => r.appSessionId === s.appSessionId)?.state === 'Closed', 'close owned window');
  }
  const original = await launch(pageA.url + '/dynamic-a');
  await caller('move', { id: original.appSessionId, rect: rectA });
  await until(() => caller('profile', original.launchUrl), p => p && near(p.normal, rectA), 'save geometry before monitored relaunch');
  await close(original);
  const a = await launch(original.launchUrl);
  assert(near((await geometry(a)).current, rectA), 'startup geometry restore');
  const b = await launch(pageB.url + '/dynamic-b');
  await caller('move', { id: b.appSessionId, rect: rectB });
  const beforeB = await geometry(b), beforeA = await geometry(a);
  await caller('monitor-start'); await active(a);
  evidence('A-visible-ACTIVE-no-capture', { result: 'PASS', monitor: await snapshot(a) });
  await start(a);
  const parked = await caller('park', { id: a.appSessionId });
  assert(outside(parked.current));
  assert(near((await geometry(b)).current, beforeB.current));
  await sample('B-fully-offscreen-monitor', a);
  evidence('native-offscreen-proof', { parked, monitors, unaffectedB: await geometry(b) });

  const nextUrl = pageB.url + '/dynamic-a-navigated';
  await cdp.extension('chrome.tabs.update(' + a.tabId + ',{url:' + JSON.stringify(nextUrl) + '})');
  await until(() => cdp.extension('chrome.tabs.get(' + a.tabId + ').then(t=>({url:t.url,status:t.status,windowId:t.windowId}))'),
    t => t.url === nextUrl && t.status === 'complete' && t.windowId === a.windowId, 'navigation while parked');
  await sample('C-navigation-while-parked', a);
  assert.equal((await geometry(a)).launchUrl, a.launchUrl);
  assert(near((await caller('profile', a.launchUrl)).normal, rectA));
  const restored = await caller('restore', { id: a.appSessionId });
  assert(near(restored.current, rectA)); assert.deepEqual(restored.identity, beforeA.identity);
  await active(a);
  evidence('D-restored-ACTIVE-monitor-detached', { result: 'PASS', restored });
  await start(b); await sample('E-selected-B-isolation', b, 5, true);
  assert(near((await geometry(a)).current, rectA));
  await start(a);

  const matrix = [];
  for (const [fps, maxWidth, maxHeight] of [[1, 960, 540], [2, 960, 540], [5, 1280, 720], [10, 1280, 720]]) {
    await start(a, { framesPerSecond: fps, maxWidth, maxHeight });
    await caller('restore', { id: a.appSessionId }); await active(a);
    await caller('park', { id: a.appSessionId });
    assert(outside((await geometry(a)).current));
    matrix.push({ fps, maxWidth, maxHeight, state: 'Parked', ...await sample('performance-parked-' + fps, a, 4) });
    await caller('restore', { id: a.appSessionId });
  }
  evidence('performance-matrix', { matrix, cpuBasis: 'percent of one CPU core; test caller includes frame-hash sampling', default: { framesPerSecond: 2, maxWidth: 240, maxHeight: 135 } });

  const stopped = await caller('monitor-stop');
  assert.deepEqual((await caller('monitor-stop')).sessions.map(s => s.generation), stopped.sessions.map(s => s.generation));
  await until(() => caller('monitor'), s => s.capturingConnections === 0, 'debugger detach on stop');
  assert.equal(await frame(a), null);
  const ownedTabs = [a.tabId, b.tabId];
  async function attached() {
    return cdp.extension('chrome.debugger.getTargets().then(ts=>ts.filter(t=>' + JSON.stringify(ownedTabs) + '.includes(t.tabId)&&t.attached).map(t=>t.tabId))');
  }
  await until(attached, list => list.length === 0, 'no owned debugger after stop');
  await start(a);
  // Force an actual worker stop; recovery must use saved binding/target cleanup and the caller's selection.
  const oldWorker = await cdp.worker();
  const page = (await cdp.call('Target.getTargets')).targetInfos.find(t => t.type === 'page' && t.url === 'about:blank');
  const { sessionId } = await cdp.call('Target.attachToTarget', { targetId: page.targetId, flatten: true });
  await cdp.call('ServiceWorker.enable', {}, sessionId);
  await cdp.call('ServiceWorker.stopAllWorkers', {}, sessionId);
  await until(async () => (await cdp.call('Target.getTargets')).targetInfos.some(t => t.targetId === oldWorker.targetId), value => !value, 'monitor worker stops');
  await cdp.call('Target.detachFromTarget', { sessionId });
  const restarted = await cdp.worker();
  assert.notEqual(restarted.targetId, oldWorker.targetId);
  await until(() => frame(a), f => f?.appSessionId === a.appSessionId && Date.now() - Date.parse(f.receivedAt) < 1500, 'monitor rehydrates after worker restart', 45000);
  await sample('worker-restart-monitor-recovery', a, 4);

  await start(b); await close(b);
  await until(() => snapshot(b), s => s.state === 'Unavailable' && !s.capturing, 'selected close cleans capture');
  assert.equal(await frame(b), null);
  assert(near((await geometry(a)).normal, rectA));
  await start(a); await caller('park', { id: a.appSessionId });
  await sample('F-integrated-final-park', a, 4);
  const finalRestore = await caller('restore', { id: a.appSessionId });
  assert(near(finalRestore.current, rectA));
  await start(a);
  // Keep one live capture during real SessionHost shutdown, verifying detach before browser cleanup.
  const shutdown = await caller('shutdown-host');
  assert.equal(shutdown.connections, 0); assert.equal(shutdown.capturingConnections, 0);
  await until(attached, list => list.length === 0, 'caller shutdown detaches all captures');
  await cdp.extension('chrome.windows.remove(' + a.windowId + ')');
  evidence('F-V0-end-to-end-and-cleanup', { result: 'PASS', shutdown, finalRestore });
  console.log('PASS: R004 production monitor, changing offscreen frames, selected-window isolation, navigation, restore, worker restart, start/stop, close and shutdown.');
}
