// Source-owned helper. Its owning PowerShell entry captures all evidence into one log.
import fs from 'node:fs/promises';
import path from 'node:path';
import os from 'node:os';
import http from 'node:http';
import { spawn } from 'node:child_process';
import { createInterface } from 'node:readline';
import { fileURLToPath } from 'node:url';
import assert from 'node:assert/strict';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const chromeExe = path.resolve(process.argv[2]);
const gui = process.argv.includes('--gui');
assert.equal(path.basename(chromeExe).toLowerCase(), 'chrome.exe');
await fs.access(chromeExe);
// Chrome's Windows singleton identity must use the same canonical path as .NET Path.GetFullPath.
const profile = await fs.realpath(await fs.mkdtemp(path.join(os.tmpdir(), 'LazyChromeExtension-R002-CfT-')));
const delay = ms => new Promise(resolve => setTimeout(resolve, ms));
async function until(read, accept, label, timeout = 20000) {
  const deadline = Date.now() + timeout;
  let value;
  do { value = await read(); if (accept(value)) return value; await delay(200); } while (Date.now() < deadline);
  throw Error('Timed out: ' + label);
}
async function localPageServer() {
  const server = http.createServer((_, response) => {
    response.writeHead(200, { 'Content-Type': 'text/html', 'Cache-Control': 'no-store' });
    response.end('<!doctype html><title>Local R002 acceptance page</title><p>Neutral local navigation target.</p>');
  });
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
  return { server, url: `http://127.0.0.1:${server.address().port}` };
}
class Cdp {
  pending = new Map(); events = []; sequence = 0;
  async open(url) {
    this.ws = new WebSocket(url);
    await new Promise((resolve, reject) => { this.ws.onopen = resolve; this.ws.onerror = reject; });
    this.ws.onmessage = event => {
      const message = JSON.parse(event.data), pending = this.pending.get(message.id);
      if (pending) {
        clearTimeout(pending.timer); this.pending.delete(message.id);
        if (message.error) pending.reject(Error(JSON.stringify(message.error))); else pending.resolve(message.result);
      } else this.events.push(message);
    };
  }
  call(method, params = {}, sessionId) {
    return new Promise((resolve, reject) => {
      const id = ++this.sequence;
      const timer = setTimeout(() => { this.pending.delete(id); reject(Error('CDP timeout: ' + method)); }, 10000);
      this.pending.set(id, { resolve, reject, timer });
      this.ws.send(JSON.stringify({ id, method, params, ...(sessionId ? { sessionId } : {}) }));
    });
  }
  async worker() {
    return until(async () => (await this.call('Target.getTargets')).targetInfos.find(t => t.type === 'service_worker' && /^chrome-extension:\/\/[^/]+\/service-worker\.js$/.test(t.url)), Boolean, 'extension worker', 45000);
  }
  async extension(expression) {
    const worker = await this.worker();
    const { sessionId } = await this.call('Target.attachToTarget', { targetId: worker.targetId, flatten: true });
    try {
      await this.call('Runtime.enable', {}, sessionId);
      await until(() => this.call('Runtime.evaluate', { expression: 'typeof chrome !== "undefined" && !!chrome.runtime?.id', returnByValue: true }, sessionId),
        ready => ready.result?.value === true, 'extension execution context');
      const result = await this.call('Runtime.evaluate', { expression, awaitPromise: true, returnByValue: true }, sessionId);
      if (result.exceptionDetails) throw Error(JSON.stringify(result.exceptionDetails));
      return result.result.value;
    } finally { await this.call('Target.detachFromTarget', { sessionId }); }
  }
}
const pageA = await localPageServer(), pageB = await localPageServer();
const cdp = new Cdp();
let browser, driver, browserExited = false, driverExited = false;
let browserStderr = '';
let driverLines = [], driverWaiters = [];
function nextDriverLine() {
  if (driverLines.length) return Promise.resolve(driverLines.shift());
  return new Promise((resolve, reject) => {
    const timer = setTimeout(() => reject(Error('Caller test driver timed out')), 10000);
    driverWaiters.push(value => { clearTimeout(timer); resolve(value); });
  });
}
async function caller(operation, argumentsOrUrl) {
  driver.stdin.write(JSON.stringify({ operation, ...(typeof argumentsOrUrl === 'string' ? { url: argumentsOrUrl } : argumentsOrUrl) }) + '\n');
  const response = JSON.parse(await nextDriverLine());
  if (response.error) throw Error(response.error);
  return response.result;
}
function evidence(label, value) { console.log(JSON.stringify({ check: label, ...value })); }
try {
  browser = spawn(chromeExe, [`--user-data-dir=${profile}`, `--load-extension=${path.join(root, 'src/Extension')}`, '--remote-debugging-port=0', '--no-first-run', '--no-default-browser-check', 'about:blank'], { stdio: ['ignore', 'ignore', 'pipe'], windowsHide: false });
  browser.on('exit', () => { browserExited = true; });
  browser.stderr.on('data', bytes => { browserStderr += bytes.toString(); });
  const port = await until(async () => {
    try { return Number((await fs.readFile(path.join(profile, 'DevToolsActivePort'), 'utf8')).split('\n')[0]); } catch { return null; }
  }, Boolean, 'debugging endpoint');
  const version = await (await fetch(`http://127.0.0.1:${port}/json/version`)).json();
  await cdp.open(version.webSocketDebuggerUrl);
  const manifest = await cdp.extension('chrome.runtime.getManifest()');
  assert.equal(manifest.name, 'LazyChromeExtension');
  assert.equal(manifest.manifest_version, 3);
  evidence('browser', { version: version.Browser, profile, manifest });
  if (gui) {
    driver = spawn(path.join(root, 'src/CallerHarness/bin/Debug/net10.0-windows/CallerHarness.exe'), ['--chrome-executable', chromeExe, '--chrome-user-data-dir', profile, '--geometry-directory', path.join(profile, 'geometry')], { cwd: root, stdio: ['pipe', 'pipe', 'pipe'], windowsHide: false });
    driver.on('exit', () => { driverExited = true; });
    driver.stderr.on('data', bytes => process.stderr.write(bytes));
    evidence('GUI-ready', { callerPid: driver.pid, launchUrlA: pageA.url + '/gui-a', launchUrlB: pageB.url + '/gui-b', debuggingPort: port, profile });
    await until(async () => driverExited, Boolean, 'operator closes CallerHarness after GUI acceptance', 900000);
    console.log('GUI fixture closed normally. Record observed GUI checks separately.');
  } else {
  driver = spawn('dotnet', [path.join(root, 'tests/CallerHarness.Tests/bin/Debug/net10.0-windows/CallerHarness.Tests.dll'), '--browser-driver', '--chrome-executable', chromeExe, '--chrome-user-data-dir', profile, '--geometry-directory', path.join(profile, 'geometry')], { cwd: root, stdio: ['pipe', 'pipe', 'pipe'], windowsHide: true });
  driver.on('exit', () => { driverExited = true; });
  driver.stderr.on('data', bytes => process.stderr.write(bytes));
  createInterface({ input: driver.stdout }).on('line', line => {
    // Chrome may inherit stdout and print its localized singleton-launch notice.
    if (!line.startsWith('R002 ')) { console.log('Caller child emitted a non-protocol launch diagnostic.'); return; }
    const payload = line.slice(5);
    if (driverWaiters.length) driverWaiters.shift()(payload); else driverLines.push(payload);
  });
  assert.equal(JSON.parse(await nextDriverLine()).ready, true);
  const a = await caller('launch', pageA.url + '/launch');
  const boundA = (await until(() => caller('sessions'), list => list.find(s => s.appSessionId === a.appSessionId)?.state === 'Bound', 'A binds'))[0];
  assert(Number.isInteger(boundA.windowId));
  const observeTab = tabId => cdp.extension(`chrome.tabs.get(${tabId}).then(t=>({id:t.id,windowId:t.windowId,url:t.url,status:t.status}))`);
  const launchedA = await until(() => observeTab(boundA.tabId), tab => tab.url === pageA.url + '/launch' && tab.status === 'complete', 'A reaches launch URL');
  assert.equal(launchedA.windowId, boundA.windowId);
  await cdp.extension(`chrome.tabs.update(${boundA.tabId}, {url:${JSON.stringify(pageB.url + '/a-navigated')}})`);
  const navigatedA = await until(() => observeTab(boundA.tabId), tab => tab.url === pageB.url + '/a-navigated' && tab.status === 'complete', 'A finishes navigation');
  assert.equal(navigatedA.windowId, boundA.windowId);
  assert.equal((await caller('sessions'))[0].windowId, boundA.windowId);
  evidence('one-session-navigation', { result: 'PASS', launchedTab: launchedA, navigatedTab: navigatedA, session: (await caller('sessions'))[0] });

  const b = await caller('launch', pageA.url + '/launch'); // Same launch URL must still mean a distinct session.
  const both = await until(() => caller('sessions'), list => list.length === 2 && list.every(s => s.state === 'Bound'), 'both sessions bind');
  const boundB = both.find(s => s.appSessionId === b.appSessionId);
  await until(() => observeTab(boundB.tabId), tab => tab.url === pageA.url + '/launch' && tab.status === 'complete', 'B reaches launch URL');
  assert.notEqual(boundA.appSessionId, boundB.appSessionId);
  assert.notEqual(boundA.windowId, boundB.windowId);
  await cdp.extension(`Promise.all([chrome.tabs.update(${boundA.tabId},{url:${JSON.stringify(pageA.url + '/a-again')}}),chrome.tabs.update(${boundB.tabId},{url:${JSON.stringify(pageB.url + '/b-navigated')}})])`);
  const tabA = await until(() => observeTab(boundA.tabId), tab => tab.url === pageA.url + '/a-again' && tab.status === 'complete', 'A second navigation');
  const tabB = await until(() => observeTab(boundB.tabId), tab => tab.url === pageB.url + '/b-navigated' && tab.status === 'complete', 'B navigation');
  assert.equal(tabA.windowId, boundA.windowId);
  assert.equal(tabB.windowId, boundB.windowId);
  const persisted = await cdp.extension('chrome.storage.session.get(null).then(data => Object.entries(data).filter(([k])=>k.startsWith("binding:")).map(([,r])=>({appSessionId:r.appSessionId,windowId:r.windowId,tabId:r.tabId})))');
  assert.equal(persisted.find(r => r.appSessionId === boundA.appSessionId).windowId, boundA.windowId);
  assert.equal(persisted.find(r => r.appSessionId === boundB.appSessionId).windowId, boundB.windowId);
  evidence('concurrent-navigation', { result: 'PASS', tabs: [tabA, tabB], sessions: await caller('sessions'), extensionBindings: persisted });
  await cdp.extension(`chrome.windows.remove(${boundA.windowId})`);
  const afterClose = await until(() => caller('sessions'), list => list.find(s => s.appSessionId === boundA.appSessionId)?.state === 'Closed', 'A closes');
  assert.equal(afterClose.find(s => s.appSessionId === boundB.appSessionId).state, 'Bound');
  assert.equal(afterClose.find(s => s.appSessionId === boundB.appSessionId).windowId, boundB.windowId);
  evidence('window-close', { result: 'PASS', sessions: afterClose });

  const oldWorker = await cdp.worker();
  const targets = (await cdp.call('Target.getTargets')).targetInfos;
  const page = targets.find(t => t.type === 'page' && t.url === 'about:blank');
  const { sessionId: pageSession } = await cdp.call('Target.attachToTarget', { targetId: page.targetId, flatten: true });
  await cdp.call('ServiceWorker.enable', {}, pageSession);
  await cdp.call('ServiceWorker.stopAllWorkers', {}, pageSession);
  await until(async () => (await cdp.call('Target.getTargets')).targetInfos.some(t => t.targetId === oldWorker.targetId), value => !value, 'old service worker stopped');
  await cdp.call('Target.detachFromTarget', { sessionId: pageSession });
  // The production alarm wakes the extension. No test-only production hook is used.
  const restarted = await cdp.worker();
  assert.notEqual(restarted.targetId, oldWorker.targetId);
  const recovered = await cdp.extension('chrome.storage.session.get(null).then(data=>Object.entries(data).filter(([k])=>k.startsWith("binding:")).map(([,r])=>({appSessionId:r.appSessionId,windowId:r.windowId,tabId:r.tabId})))');
  assert.equal(recovered.length, 1);
  assert.equal(recovered[0].appSessionId, boundB.appSessionId);
  assert.equal(recovered[0].windowId, boundB.windowId);
  await cdp.extension(`chrome.tabs.update(${boundB.tabId},{url:${JSON.stringify(pageA.url + '/after-worker-restart')}})`);
  await until(() => observeTab(boundB.tabId), tab => tab.url === pageA.url + '/after-worker-restart' && tab.status === 'complete', 'navigation after worker restart');
  const finalSessions = await caller('sessions');
  assert.equal(finalSessions.find(s => s.appSessionId === boundB.appSessionId).windowId, boundB.windowId);
  assert.equal(finalSessions.find(s => s.appSessionId === boundB.appSessionId).state, 'Bound');
  evidence('mv3-stop-and-alarm-restart', { result: 'PASS', oldTargetId: oldWorker.targetId, newTargetId: restarted.targetId, recovered, sessions: finalSessions });
  await cdp.extension(`chrome.windows.remove(${boundB.windowId})`);
  await until(() => caller('sessions'), list => list.every(s => s.state === 'Closed'), 'both sessions close');
  console.log('PASS: real Chrome integration, same-URL concurrent sessions, cross-origin navigation, close, and MV3 restart.');
  if (process.argv.includes('--geometry')) {
    const { testGeometry } = await import('./Test-R003.mjs');
    await testGeometry({ caller, cdp, until, delay, evidence, pageA, pageB });
  }
  }
} catch (error) {
  process.exitCode = 1;
  console.error(error.stack);
  if (driver && !driverExited && !gui) evidence('failed-caller-state', { sessions: await caller('sessions') });
  if (cdp.ws?.readyState === WebSocket.OPEN) {
    const targets = (await cdp.call('Target.getTargets')).targetInfos;
    evidence('failed-targets', { targets: targets.map(t => ({ type: t.type, url: t.url.split('#')[0] })) });
    evidence('failed-extension-bindings', { records: await cdp.extension('chrome.storage.session.get(null).then(d=>Object.entries(d).filter(([k])=>k.startsWith("binding:")).map(([,r])=>({appSessionId:r.appSessionId,windowId:r.windowId,closed:r.closed})))') });
    for (const target of targets.filter(t => t.type === 'page' && t.url.includes('/lazy-chrome-extension/bootstrap'))) {
      const { sessionId } = await cdp.call('Target.attachToTarget', { targetId: target.targetId, flatten: true });
      await cdp.call('Runtime.enable', {}, sessionId);
      await delay(200);
      const contexts = cdp.events.filter(e => e.sessionId === sessionId && e.method === 'Runtime.executionContextCreated').map(e => e.params.context);
      for (const context of contexts.filter(c => c.origin.startsWith('chrome-extension://'))) {
        const diagnostic = await cdp.call('Runtime.evaluate', { contextId: context.id, expression: 'chrome.runtime.sendMessage({type:"bind-bootstrap",url:location.href})', awaitPromise: true, returnByValue: true }, sessionId);
        evidence('bootstrap-diagnostic', { response: diagnostic.result?.value, exception: diagnostic.exceptionDetails?.text });
      }
      await cdp.call('Target.detachFromTarget', { sessionId });
    }
  }
} finally {
  if (driver && !driverExited) { driver.stdin.write('{"operation":"stop"}\n'); driver.stdin.end(); }
  if (cdp.ws?.readyState === WebSocket.OPEN) { try { await cdp.call('Browser.close'); } catch {} cdp.ws.close(); }
  for (let i = 0; i < 50 && ((!browserExited && browser) || (!driverExited && driver)); i++) await delay(100);
  if ((browser && !browserExited) || (driver && !driverExited)) {
    process.exitCode = 1;
    for (const owned of [!browserExited && browser, !driverExited && driver].filter(Boolean))
      await new Promise(resolve => spawn('taskkill', ['/PID', String(owned.pid), '/T', '/F'], { windowsHide: true }).on('exit', resolve));
  }
  pageA.server.closeAllConnections(); pageA.server.close();
  pageB.server.closeAllConnections(); pageB.server.close();
  evidence('cleanup', { browserExited, driverExited, profileRetained: profile });
  const diagnostics = browserStderr.split(/\r?\n/).filter(line => /error|failed/i.test(line));
  if (diagnostics.length) evidence('browser-diagnostics', { lines: diagnostics.map(line => line.replace(/#v=1\S*/g, '#[redacted]')) });
}
