// Focused real CfT acceptance for the bounded public key-chord API. The local page
// is test instrumentation only; production does not inspect post-input page state.
import assert from 'node:assert/strict';
import { observeMonitor } from './MonitorContinuity.mjs';

export async function testKeyChord({ caller, cdp, until, delay, evidence, pageA }) {
  const url = pageA.url + '/key-chord';
  const geometry = session => caller('geometry', { id: session.appSessionId });
  const snapshot = async session => (await caller('monitor')).sessions.find(row => row.appSessionId === session.appSessionId);
  const attachment = tabId => cdp.extension(`chrome.debugger.getTargets().then(ts=>ts.filter(t=>t.tabId===${tabId}).map(t=>({tabId:t.tabId,attached:t.attached})))`);
  const audit = await observeMonitor(cdp);
  const launched = await caller('launch', url);
  const session = (await until(() => caller('sessions'), rows => rows.find(row => row.appSessionId === launched.appSessionId)?.state === 'Bound', 'key fixture binds'))[0];
  await until(() => cdp.extension(`chrome.tabs.get(${session.tabId}).then(t=>({url:t.url,status:t.status,windowId:t.windowId}))`),
    tab => tab.url === url && tab.status === 'complete' && tab.windowId === session.windowId, 'key fixture reaches owned active tab');
  await until(() => snapshot(session), row => row?.connected === true, 'authenticated control connection');
  const beforeOff = { session: (await caller('sessions')).find(row => row.appSessionId === session.appSessionId), geometry: await geometry(session), monitor: await snapshot(session), audit: await audit() };
  assert.equal((await caller('monitor')).enabled, false);
  assert(!(await attachment(session.tabId)).some(target => target.attached));
  pageA.events.length = 0;
  assert.deepEqual(await caller('key-chord', { id: session.appSessionId, chord: { key: 'PageDown', modifiers: 'None' } }), { dispatched: true });
  const pageDown = await until(() => pageA.events.find(event => event.kind === 'keydown' && event.key === 'PageDown'), Boolean, 'PageDown reaches local fixture');
  const afterPageDown = await until(() => pageA.events.find(event => event.kind === 'post-key' && Number(event.scrollY) > 0), Boolean, 'PageDown advances local fixture scroll');
  const afterOff = { session: (await caller('sessions')).find(row => row.appSessionId === session.appSessionId), geometry: await geometry(session), monitor: await snapshot(session), audit: await audit() };
  assert.equal(afterOff.session.appSessionId, beforeOff.session.appSessionId); assert.equal(afterOff.session.windowId, beforeOff.session.windowId);
  assert.deepEqual(afterOff.geometry.identity, beforeOff.geometry.identity); assert.deepEqual(afterOff.geometry.current, beforeOff.geometry.current); assert.deepEqual(afterOff.geometry.normal, beforeOff.geometry.normal);
  assert.equal((await caller('monitor')).enabled, false); assert.equal(afterOff.monitor?.generation, beforeOff.monitor?.generation);
  assert.equal((await attachment(session.tabId)).some(target => target.attached), false);
  assert.equal((afterOff.audit.tabs[session.tabId]?.attaches ?? 0) - (beforeOff.audit.tabs[session.tabId]?.attaches ?? 0), 1);
  assert.equal((afterOff.audit.tabs[session.tabId]?.detaches ?? 0) - (beforeOff.audit.tabs[session.tabId]?.detaches ?? 0), 1);
  evidence('key-chord-visible-monitor-off', { result: 'PASS', session: afterOff.session, geometry: afterOff.geometry, pageDown, afterPageDown, temporaryDebuggerDetached: true });

  await caller('monitor-start', { options: { mode: 'BrowserViewport', framesPerSecond: 2, maxWidth: 240, maxHeight: 135 } });
  const live = await until(() => snapshot(session), row => row?.state === 'Live' && row.capturing, 'BrowserViewport monitor becomes live', 45000);
  const firstFrame = await until(() => caller('monitor-frame', { id: session.appSessionId }), Boolean, 'BrowserViewport fresh frame', 45000);
  const beforeOn = { generation: live.generation, geometry: await geometry(session), socket: (await audit()).windows[session.windowId]?.socket,
    attaches: (await audit()).tabs[session.tabId]?.attaches ?? 0, detaches: (await audit()).tabs[session.tabId]?.detaches ?? 0, sequence: firstFrame.sequence };
  assert((await attachment(session.tabId)).some(target => target.attached));
  pageA.events.length = 0;
  assert.deepEqual(await caller('key-chord', { id: session.appSessionId, chord: { key: 'Enter', modifiers: 'Ctrl' } }), { dispatched: true });
  const ctrlEnter = await until(() => pageA.events.find(event => event.kind === 'keydown' && event.key === 'Enter' && event.ctrl === 'true' && event.shift === 'false'), Boolean, 'Ctrl+Enter reaches local fixture');
  const afterOn = await until(async () => {
    const row = await snapshot(session); const frame = await caller('monitor-frame', { id: session.appSessionId }); const currentAudit = await audit();
    return row?.generation === beforeOn.generation && frame?.sequence > beforeOn.sequence && currentAudit.windows[session.windowId]?.socket === beforeOn.socket ? { row, frame, currentAudit } : null;
  }, Boolean, 'monitor remains fresh after key dispatch', 45000);
  const afterOnGeometry = await geometry(session);
  assert.deepEqual(afterOnGeometry.identity, beforeOn.geometry.identity); assert.deepEqual(afterOnGeometry.current, beforeOn.geometry.current); assert.deepEqual(afterOnGeometry.normal, beforeOn.geometry.normal);
  assert.equal(afterOn.currentAudit.tabs[session.tabId]?.attaches ?? 0, beforeOn.attaches); assert.equal(afterOn.currentAudit.tabs[session.tabId]?.detaches ?? 0, beforeOn.detaches);
  assert((await attachment(session.tabId)).some(target => target.attached));
  evidence('key-chord-browser-viewport-monitor-live', { result: 'PASS', ctrlEnter, generation: afterOn.row.generation, firstSequence: beforeOn.sequence, freshSequence: afterOn.frame.sequence, socket: beforeOn.socket, geometry: afterOnGeometry });
  await caller('monitor-stop');
  await until(() => attachment(session.tabId), targets => !targets.some(target => target.attached), 'BrowserViewport debugger detaches after stop');
  evidence('key-chord-monitor-cleanup', { result: 'PASS', monitor: await caller('monitor'), attachment: await attachment(session.tabId) });
  await cdp.extension(`chrome.windows.remove(${session.windowId})`);
  await until(() => caller('sessions'), rows => rows.find(row => row.appSessionId === session.appSessionId)?.state === 'Closed', 'key fixture window closes');
  await delay(100);
  console.log('PASS: focused real key-chord acceptance: exact owned PageDown temporary attach/detach and Ctrl+Enter monitor attachment reuse.');
}
