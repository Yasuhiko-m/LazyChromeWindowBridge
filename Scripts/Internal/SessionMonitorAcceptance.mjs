// Source-owned real public-API acceptance, invoked by the existing five-window fixture.
import assert from 'node:assert/strict';

export async function testSessionControl({ caller, cdp, until, delay, evidence, audit }, rows) {
  const [a, ...peers] = rows;
  const frame = s => caller('monitor-frame', { id: s.appSessionId });
  const monitor = () => caller('monitor');
  const row = (m, s) => m.sessions.find(r => r.appSessionId === s.appSessionId);
  const set = enabled => caller('monitor-session', { id: a.appSessionId, enabled });
  const attached = () => cdp.extension('chrome.debugger.getTargets().then(ts=>ts.filter(t=>t.attached).map(t=>t.tabId))');
  const before = await monitor(), originalAudit = await audit();
  const geometry = await caller('geometry', { id: a.appSessionId });
  assert(rows.every(s => row(before, s).state === 'Live'));
  await set(false);
  await until(attached, ids => !ids.includes(a.tabId) && peers.every(s => ids.includes(s.tabId)), 'only paused debugger detaches');
  const frozen = await frame(a), paused = await monitor();
  assert(frozen && frozen.jpegSha256);
  assert.equal(row(paused, a).state, 'Paused'); assert.equal(row(paused, a).capturing, false);
  assert.equal(row(paused, a).connected, true); assert.equal(paused.connections, before.connections);
  assert.deepEqual(await caller('geometry', { id: a.appSessionId }), geometry);
  const peerAudit = await audit();
  async function assertPaused(label) {
    await delay(1600);
    const current = await monitor(), observed = await audit();
    assert.equal(row(current, a).state, 'Paused'); assert.equal(row(current, a).capturing, false);
    assert.equal(row(current, a).generation, row(paused, a).generation);
    assert.deepEqual(await frame(a), frozen, 'frozen JPEG identity/bytes/sequence/time stay exact');
    assert.equal(current.connections, before.connections);
    for (const s of peers) {
      assert.equal(row(current, s).state, 'Live'); assert(row(current, s).frames > row(paused, s).frames);
      assert.equal(row(current, s).generation, row(before, s).generation);
      assert.equal(observed.windows[s.windowId].socket, originalAudit.windows[s.windowId].socket);
      assert.equal(observed.tabs[s.tabId].attaches, peerAudit.tabs[s.tabId].attaches);
      assert.equal(observed.tabs[s.tabId].detaches, peerAudit.tabs[s.tabId].detaches);
    }
    assert.equal(observed.tabs[a.tabId].attaches, originalAudit.tabs[a.tabId].attaches);
    assert.equal(observed.tabs[a.tabId].detaches, originalAudit.tabs[a.tabId].detaches + 1);
    evidence(label, { result: 'PASS', paused: row(current, a), frozen, peers: peers.map(s => row(current, s)), audit: observed });
  }
  await assertPaused('session-pause-frozen-peer-isolation');
  await caller('park', { id: a.appSessionId }); await assertPaused('paused-park-no-auto-resume');
  await caller('restore', { id: a.appSessionId }); await assertPaused('paused-restore-no-auto-resume');
  await caller('monitor-start', { options: { framesPerSecond: 5, maxWidth: 320, maxHeight: 180 } });
  await assertPaused('paused-global-options-no-auto-resume');
  await set(true);
  await until(() => frame(a), f => f?.sequence > frozen.sequence, 'resumed fresh JPEG');
  const resumed = await monitor(), resumeAudit = await audit();
  assert.equal(row(resumed, a).state, 'Live');
  assert.equal(resumeAudit.windows[a.windowId].socket, originalAudit.windows[a.windowId].socket);
  assert.equal(resumeAudit.tabs[a.tabId].attaches, originalAudit.tabs[a.tabId].attaches + 1);
  assert.equal(resumeAudit.tabs[a.tabId].detaches, originalAudit.tabs[a.tabId].detaches + 1);
  for (const s of peers) {
    assert.equal(row(resumed, s).generation, row(before, s).generation);
    assert.equal(resumeAudit.windows[s.windowId].socket, originalAudit.windows[s.windowId].socket);
    assert.equal(resumeAudit.tabs[s.tabId].attaches, originalAudit.tabs[s.tabId].attaches);
    assert.equal(resumeAudit.tabs[s.tabId].detaches, originalAudit.tabs[s.tabId].detaches);
  }
  evidence('session-resume-same-socket', { result: 'PASS', monitor: resumed, fresh: await frame(a), audit: resumeAudit });
  await set(false);
  await caller('monitor-stop');
  await until(monitor, m => m.capturingConnections === 0, 'global stop capture cleanup');
  await until(attached, ids => rows.every(s => !ids.includes(s.tabId)), 'global stop debugger cleanup');
  const stopped = await monitor();
  assert.equal(stopped.enabled, false); assert.equal(stopped.connections, before.connections);
  for (const s of rows) assert.equal(await frame(s), null);
  await delay(800); assert.equal((await monitor()).frames, stopped.frames);
  await assert.rejects(set(true), /Start global monitoring/);
  evidence('session-global-stop-clear-retain-sockets', { result: 'PASS', monitor: stopped });
  await caller('monitor-start');
  await until(monitor, m => rows.every(s => row(m, s).state === 'Live'), 'global batch restart all sessions');
  const restarted = await monitor(), restartAudit = await audit();
  for (const s of rows) assert.equal(restartAudit.windows[s.windowId].socket, originalAudit.windows[s.windowId].socket);
  evidence('session-global-batch-restart', { result: 'PASS', monitor: restarted, audit: restartAudit });
}
