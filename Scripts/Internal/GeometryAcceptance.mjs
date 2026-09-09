// Source-owned geometry acceptance, using the established LCWB real browser/production caller fixture.
import assert from 'node:assert/strict';

export async function testGeometry({ caller, cdp, until, delay, evidence, pageA, pageB }) {
  const monitors = await caller('monitors');
  assert(monitors.length > 0);
  evidence('native-monitor-topology', { monitors, coordinates: 'physical pixels; Win32 per-monitor-v2 scope' });
  const work = monitors.find(m => m.primary).workArea;
  const rectA = { left: work.left + 40, top: work.top + 50, width: Math.min(880, work.width - 100), height: Math.min(650, work.height - 110) };
  const workB = (monitors.find(m => m.bounds.left < 0) ?? monitors.find(m => !m.primary) ?? monitors.find(m => m.primary)).workArea;
  const rectB = { left: workB.left + 90, top: workB.top + 90, width: Math.min(760, workB.width - 150), height: Math.min(560, workB.height - 150) };
  const urlA = pageA.url + '/geometry-a?profile=1', urlB = pageA.url + '/geometry-b?profile=2';
  const near = (actual, expected) => actual && ['left', 'top', 'width', 'height'].every(k => Math.abs(actual[k] - expected[k]) <= 2);
  const outside = rect => monitors.every(m => rect.left + rect.width <= m.bounds.left || rect.left >= m.bounds.left + m.bounds.width || rect.top + rect.height <= m.bounds.top || rect.top >= m.bounds.top + m.bounds.height);
  const geometry = session => caller('geometry', { id: session.appSessionId });
  const command = (op, session) => caller(op, { id: session.appSessionId });
  async function launch(url) {
    const created = await caller('launch', url);
    const session = (await until(() => caller('sessions'), list => list.find(s => s.appSessionId === created.appSessionId)?.state === 'Bound', 'geometry bind')).find(s => s.appSessionId === created.appSessionId);
    await until(() => geometry(session), g => g?.state === 'Visible', 'native mapping');
    await until(() => cdp.extension(`chrome.tabs.get(${session.tabId}).then(t=>({url:t.url,status:t.status}))`), t => t.url === url && t.status === 'complete', 'launch transition after native mapping');
    return session;
  }
  async function close(session) {
    await cdp.extension(`chrome.windows.remove(${session.windowId})`);
    await until(() => caller('sessions'), list => list.find(s => s.appSessionId === session.appSessionId)?.state === 'Closed', 'geometry window close');
  }
  async function moveSave(session, rect) {
    await caller('move', { id: session.appSessionId, rect });
    const profile = await until(() => caller('profile', session.launchUrl), p => near(p?.normal, rect), 'debounced normal profile');
    assert(near((await geometry(session)).current, rect));
    return profile;
  }
  const a1 = await launch(urlA);
  const savedA = await moveSave(a1, rectA);
  await close(a1);
  const a = await launch(urlA);
  const restoredStartup = await geometry(a);
  assert(near(restoredStartup.current, rectA));
  evidence('A-save-close-relaunch', { result: 'PASS', savedA, restoredStartup, tolerancePixels: 2 });
  const b = await launch(urlB);
  const savedB = await moveSave(b, rectB);
  const beforeB = await geometry(b), beforeA = await geometry(a);
  assert.notEqual(beforeA.identity.hwnd, beforeB.identity.hwnd);
  assert.notEqual(a.windowId, b.windowId);
  assert(near((await caller('profile', urlA)).normal, rectA));
  assert(near((await caller('profile', urlB)).normal, rectB));
  evidence('B-independent-profiles', { result: 'PASS', savedA: await caller('profile', urlA), savedB });
  const parked = await command('park', a);
  assert.equal(parked.state, 'Parked');
  assert.equal(parked.identity.hwnd, beforeA.identity.hwnd);
  assert(outside(parked.current));
  await delay(1800); // More than the production debounce, checking against accidental PARK learning.
  const duplicate = await command('park', a);
  assert(near(duplicate.current, parked.current));
  assert(near((await geometry(b)).current, beforeB.current));
  assert(near((await caller('profile', urlA)).normal, rectA));
  evidence('C-native-park-isolation', { result: 'PASS', beforeA, parked: await geometry(a), unaffectedB: await geometry(b), intersectsAnyMonitor: false });
  const navigatedUrl = pageB.url + '/navigation-while-parked';
  await cdp.extension(`chrome.tabs.update(${a.tabId},{url:${JSON.stringify(navigatedUrl)}})`);
  const navigated = await until(() => cdp.extension(`chrome.tabs.get(${a.tabId}).then(t=>({url:t.url,status:t.status,windowId:t.windowId}))`), t => t.url === navigatedUrl && t.status === 'complete', 'navigation while parked');
  assert.equal(navigated.windowId, a.windowId);
  assert.equal((await geometry(a)).state, 'Parked');
  const restored = await command('restore', a);
  assert.equal(restored.identity.hwnd, beforeA.identity.hwnd);
  assert.equal(restored.windowId, a.windowId);
  assert.equal(restored.launchUrl, urlA);
  assert.equal(restored.state, 'Visible');
  assert(near(restored.current, rectA));
  assert(near((await command('restore', a)).current, rectA));
  assert(near((await geometry(b)).current, beforeB.current));
  evidence('D-restore-after-parked-navigation', { result: 'PASS', navigated, restored, maxErrorPixels: Math.max(...Object.keys(rectA).map(k => Math.abs(restored.current[k] - rectA[k]))) });
  await close(a);
  const a2 = await launch(urlA);
  assert(near((await geometry(a2)).current, rectA));
  assert(near((await caller('profile', urlA)).normal, rectA));
  evidence('E-relaunch-after-park', { result: 'PASS', session: a2, geometry: await geometry(a2) });
  await command('park', a2);
  await close(a2);
  await until(() => geometry(a2), g => g.state === 'Closed', 'native close while parked');
  await assert.rejects(command('restore', a2));
  assert(near((await caller('profile', urlA)).normal, rectA));
  assert(near((await geometry(b)).current, rectB));
  await close(b);
  const b2 = await launch(urlB);
  assert(near((await geometry(b2)).current, rectB));
  await close(b2);
  evidence('closed-while-parked-and-B-relaunch', { result: 'PASS', closedA: await geometry(a2), profileA: await caller('profile', urlA), profileB: await caller('profile', urlB) });
  console.log('PASS: geometry real native geometry persistence, independent profiles, PARK/RESTORE, concurrent isolation, parked navigation, relaunch and parked close.');
}
