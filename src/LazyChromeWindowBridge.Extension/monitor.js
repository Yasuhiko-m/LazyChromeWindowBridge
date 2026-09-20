// Human-view-only pixel transport. No DOM/Runtime/Network commands or image analysis.
const TARGET = 'monitor-target:';
const MODIFIER_BITS = { Alt: 1, Ctrl: 2, Meta: 4, Shift: 8 };
const SPECIAL_KEYS = {
  PageDown: ['PageDown', 'PageDown', 34], PageUp: ['PageUp', 'PageUp', 33], Enter: ['Enter', 'Enter', 13], Tab: ['Tab', 'Tab', 9], Escape: ['Escape', 'Escape', 27], Space: [' ', 'Space', 32],
  Home: ['Home', 'Home', 36], End: ['End', 'End', 35], ArrowUp: ['ArrowUp', 'ArrowUp', 38], ArrowDown: ['ArrowDown', 'ArrowDown', 40], ArrowLeft: ['ArrowLeft', 'ArrowLeft', 37], ArrowRight: ['ArrowRight', 'ArrowRight', 39],
  Backspace: ['Backspace', 'Backspace', 8], Delete: ['Delete', 'Delete', 46]
};
const KEY_ALLOWLIST = Object.freeze(Object.fromEntries([
  ...Object.entries(SPECIAL_KEYS),
  ...'ABCDEFGHIJKLMNOPQRSTUVWXYZ'.split('').map(key => [key, [key, `Key${key}`, key.charCodeAt(0)]]),
  ...'0123456789'.split('').map(key => [`Digit${key}`, [key, `Digit${key}`, key.charCodeAt(0)]])
]));
const REQUEST_ID = /^[a-f0-9]{32}$/;

export function validKeyChordRequest(request) {
  return request && typeof request === 'object' && REQUEST_ID.test(request.requestId ?? '') && typeof request.key === 'string' &&
    Object.hasOwn(KEY_ALLOWLIST, request.key) && Array.isArray(request.modifiers) && request.modifiers.length <= 4 &&
    request.modifiers.every(value => typeof value === 'string' && Object.hasOwn(MODIFIER_BITS, value)) && new Set(request.modifiers).size === request.modifiers.length;
}
export class MonitorManager {
  constructor(browser, Socket = globalThis.WebSocket, processor = processScreenshot) {
    this.browser = browser; this.Socket = Socket; this.processor = processor; this.connections = new Map();
    browser.debugger.onDetach.addListener((target, reason) => {
      for (const client of this.connections.values()) {
        if (client.tabId === target.tabId && !client.detaching) {
          if (reason === 'target_closed') {
            // Closing a tab does not relinquish the already-owned window.
            client.closedTabId = target.tabId; client.tabId = null; client.wake?.();
            continue;
          }
          client.tabId = null; client.blocked = client.control?.generation;
          client.send({ type: 'status', generation: client.control?.generation ?? 0, capturing: false,
            error: `Monitor detached (${reason}). Press Start monitor to retry.` });
        }
      }
    });
  }
  async recover() {
    const stored = await this.browser.storage.session.get(null);
    for (const [key, value] of Object.entries(stored).filter(([key]) => key.startsWith(TARGET))) {
      // Only IDs previously attached by this extension are eligible for cleanup.
      try { await this.browser.debugger.detach({ tabId: value.tabId }); } catch { }
      await this.browser.storage.session.remove(key);
    }
  }
  ensure(record) {
    if (!record.monitoring || record.closed || this.connections.has(record.appSessionId)) return;
    const socket = new this.Socket(record.origin.replace('http:', 'ws:') + '/lazy-chrome-window-bridge/monitor');
    const client = { record, socket, tabId: null, control: null, running: false, removed: false, blocked: null, detaching: false, keyRequests: new Set(), keyResults: new Map(),
      send: message => { if (socket.readyState === 1) socket.send(JSON.stringify(message)); } };
    this.connections.set(record.appSessionId, client);
    socket.onopen = () => client.send({ appSessionId: record.appSessionId, token: record.token });
    socket.onmessage = event => {
      try {
        const control = JSON.parse(event.data), settings = control.options;
        if (!Number.isSafeInteger(control.generation) || typeof control.enabled !== 'boolean' ||
            !Number.isInteger(settings.framesPerSecond) || settings.framesPerSecond < 1 || settings.framesPerSecond > 30 ||
            !Number.isInteger(settings.maxWidth) || settings.maxWidth < 160 || settings.maxWidth > 1920 ||
            !Number.isInteger(settings.maxHeight) || settings.maxHeight < 90 || settings.maxHeight > 1080 ||
            !Number.isInteger(settings.mode) || ![0, 1].includes(settings.mode) || (settings.mode === 1 && control.enabled) || !validOptions(settings)) throw Error('Invalid monitor control.');
        const previous = client.control;
        client.control = control;
        // Core serializes a null optional request while the control socket is idle.
        // Null is not a request and must keep the authenticated control connection alive.
        if (control.keyChord !== undefined && control.keyChord !== null) {
          if (!validKeyChordRequest(control.keyChord)) throw Error('Invalid key chord control.');
          void this.dispatchKeyChord(client, control.keyChord);
        }
        if (typeof control.sourceSizeRequestId === 'string' && control.sourceSizeRequestId.length === 32) void this.probeSourceSize(client, control.sourceSizeRequestId);
        if (previous?.generation !== control.generation || previous?.enabled !== control.enabled || control.closing ||
            previous?.options.framesPerSecond !== settings.framesPerSecond || previous?.options.maxWidth !== settings.maxWidth ||
            previous?.options.maxHeight !== settings.maxHeight || JSON.stringify(previous?.options.region) !== JSON.stringify(settings.region) ||
            JSON.stringify(previous?.options.resize) !== JSON.stringify(settings.resize)) client.wake?.();
        void this.pump(client);
      } catch { this.remove(record.appSessionId); }
    };
    socket.onclose = () => {
      client.removed = true;
      client.wake?.();
      if (this.connections.get(record.appSessionId) === client) this.connections.delete(record.appSessionId);
      void this.pump(client);
    };
    socket.onerror = () => socket.close();
  }
  async dispatchKeyChord(client, request) {
    const prior = client.keyResults.get(request.requestId);
    if (prior) { client.send(prior); return; }
    if (client.keyRequests.has(request.requestId)) return;
    client.keyRequests.add(request.requestId);
    const finish = result => {
      client.keyResults.set(request.requestId, result);
      if (client.keyResults.size > 16) client.keyResults.delete(client.keyResults.keys().next().value);
      client.send(result);
    };
    let tabId = client.tabId, attachedHere = false;
    try {
      let tab;
      if (tabId === null) {
        const tabs = await this.browser.tabs.query({ windowId: client.record.windowId, active: true });
        if (tabs.length !== 1 || tabs[0].windowId !== client.record.windowId || tabs[0].incognito) throw Error('Owned window has no eligible active tab.');
        tabId = tabs[0].id; await this.browser.debugger.attach({ tabId }, '1.3'); attachedHere = true;
      }
      tab = await this.browser.tabs.get(tabId);
      if (tab.windowId !== client.record.windowId || !tab.active || tab.incognito) throw Error('Owned active tab changed.');
      const [key, code, virtualKey] = KEY_ALLOWLIST[request.key];
      const modifiers = request.modifiers.reduce((bits, modifier) => bits | MODIFIER_BITS[modifier], 0);
      const fixed = { key, code, windowsVirtualKeyCode: virtualKey, nativeVirtualKeyCode: virtualKey, modifiers };
      await this.browser.debugger.sendCommand({ tabId }, 'Input.dispatchKeyEvent', { type: 'keyDown', ...fixed });
      await this.browser.debugger.sendCommand({ tabId }, 'Input.dispatchKeyEvent', { type: 'keyUp', ...fixed });
      finish({ type: 'key-chord-result', requestId: request.requestId, success: true });
    } catch (error) { finish({ type: 'key-chord-result', requestId: request.requestId, success: false, error: String(error.message).slice(0, 240) }); }
    finally {
      if (attachedHere) try { await this.browser.debugger.detach({ tabId }); } catch { }
      client.keyRequests.delete(request.requestId);
    }
  }
  async probeSourceSize(client, requestId) {
    let tabId = client.tabId, attachedHere = false;
    try {
      if (tabId === null) {
        const tabs = await this.browser.tabs.query({ windowId: client.record.windowId, active: true });
        if (tabs.length !== 1 || tabs[0].windowId !== client.record.windowId || tabs[0].incognito) throw Error('Owned window has no eligible active tab.');
        tabId = tabs[0].id; await this.browser.debugger.attach({ tabId }, '1.3'); attachedHere = true;
      }
      const viewport = (await this.browser.debugger.sendCommand({ tabId }, 'Page.getLayoutMetrics')).cssVisualViewport;
      const current = await this.browser.tabs.get(tabId);
      if (current.windowId !== client.record.windowId || !current.active || !Number.isFinite(viewport.clientWidth) || !Number.isFinite(viewport.clientHeight) || viewport.clientWidth <= 0 || viewport.clientHeight <= 0) throw Error('Invalid viewport extent.');
      client.send({ type: 'source-size', requestId, width: viewport.clientWidth, height: viewport.clientHeight });
    } catch (error) { client.send({ type: 'source-size', requestId, error: String(error.message).slice(0, 240) }); }
    finally { if (attachedHere) try { await this.browser.debugger.detach({ tabId }); } catch { } }
  }
  remove(id) {
    const client = this.connections.get(id);
    if (!client) return;
    client.removed = true; client.wake?.(); client.socket.close(); void this.pump(client);
  }
  async detach(client) {
    if (client.tabId === null) { await this.browser.storage.session.remove(TARGET + client.record.appSessionId); return; }
    const tabId = client.tabId; client.detaching = true;
    try { await this.browser.debugger.detach({ tabId }); } catch { }
    finally { client.tabId = null; client.detaching = false; await this.browser.storage.session.remove(TARGET + client.record.appSessionId); }
  }
  async pump(client) {
    if (client.running) return;
    client.running = true;
    let activeGeneration = client.control?.generation;
    let activeTabId = null;
    try {
      while (!client.removed && client.control?.enabled && client.blocked !== client.control.generation) {
        const control = client.control, started = performance.now();
        activeGeneration = control.generation;
        const tabs = await this.browser.tabs.query({ windowId: client.record.windowId, active: true });
        if (tabs.length !== 1 || tabs[0].windowId !== client.record.windowId || tabs[0].incognito) throw Error('Owned window has no eligible active tab.');
        const tabId = tabs[0].id;
        activeTabId = tabId;
        if (client.tabId !== tabId) {
          await this.detach(client);
          await this.browser.storage.session.set({ [TARGET + client.record.appSessionId]: { tabId } });
          await this.browser.debugger.attach({ tabId }, '1.3');
          client.tabId = tabId;
          client.closedTabId = null;
        }
        if (client.removed || !client.control.enabled || client.blocked === control.generation || client.control.generation !== control.generation) break;
        const viewport = (await this.browser.debugger.sendCommand({ tabId }, 'Page.getLayoutMetrics')).cssVisualViewport;
        const capture = this.browser.debugger.sendCommand({ tabId }, 'Page.captureScreenshot', {
          format: 'png', captureBeyondViewport: false
        });
        let timeout;
        const frame = await Promise.race([capture, new Promise((_, reject) => { timeout = setTimeout(() => reject(Error('Monitor capture timed out.')), 5000); })]).finally(() => clearTimeout(timeout));
        const current = await this.browser.tabs.get(tabId);
        if (client.removed || !client.control.enabled || client.blocked === control.generation || control.generation !== client.control.generation) break;
        if (current.windowId !== client.record.windowId || !current.active) { await this.detach(client); continue; }
        const processed = await this.processor(frame.data, viewport.clientWidth, viewport.clientHeight, control.options);
        if (processed.data.length > 2800000) throw Error('Monitor frame exceeds transport bounds.');
        if (client.socket.bufferedAmount < 1000000) client.send({ type: 'frame', generation: control.generation,
          windowId: client.record.windowId, tabId, identity: control.identity, data: processed.data, captureMilliseconds: performance.now() - started });
        await new Promise(resolve => {
          const timer = setTimeout(resolve, Math.max(0, 1000 / client.control.options.framesPerSecond - (performance.now() - started)));
          client.wake = () => { clearTimeout(timer); resolve(); };
        });
        client.wake = null;
      }
    } catch (error) {
      if (client.closedTabId !== activeTabId || activeTabId === null) {
        client.blocked = activeGeneration;
        client.send({ type: 'status', generation: activeGeneration ?? 0, capturing: false, error: String(error.message).slice(0, 240) });
      }
    } finally {
      await this.detach(client);
      client.send({ type: 'status', generation: client.control?.generation ?? 0, capturing: false });
      client.running = false;
      if (client.control?.closing && !client.removed) { client.removed = true; client.socket.close(); }
      // A session may resume while its previous acquisition/detach is finishing.
      // Continue only after that cleanup, on the existing waiting socket.
      if (!client.removed && client.control?.enabled && client.blocked !== client.control.generation) {
        client.closedTabId = null;
        void this.pump(client);
      }
    }
  }
}

function validOptions(options) {
  const region = options.region ?? { columns: 1, rows: 1, column: 0, row: 0, columnSpan: 1, rowSpan: 1 };
  if (![region.columns, region.rows, region.column, region.row, region.columnSpan, region.rowSpan].every(Number.isInteger) ||
      region.columns < 1 || region.columns > 64 || region.rows < 1 || region.rows > 64 || region.column < 0 || region.row < 0 || region.columnSpan < 1 || region.rowSpan < 1 ||
      region.column + region.columnSpan > region.columns || region.row + region.rowSpan > region.rows) return false;
  const resize = options.resize;
  return resize === null || resize === undefined || (Number.isInteger(resize.filter) && [0, 1, 2].includes(resize.filter) &&
    (resize.width === null || resize.width === undefined || Number.isInteger(resize.width) && resize.width > 0 && resize.width <= 8192) &&
    (resize.height === null || resize.height === undefined || Number.isInteger(resize.height) && resize.height > 0 && resize.height <= 8192));
}

export function resolveCaptureTransform(sourceWidth, sourceHeight, options) {
  if (!Number.isInteger(sourceWidth) || !Number.isInteger(sourceHeight) || sourceWidth < 1 || sourceHeight < 1 || !validOptions(options)) throw Error('Invalid capture processing options.');
  const region = options.region ?? { columns: 1, rows: 1, column: 0, row: 0, columnSpan: 1, rowSpan: 1 };
  const x = Math.floor(sourceWidth * region.column / region.columns), y = Math.floor(sourceHeight * region.row / region.rows);
  const right = Math.floor(sourceWidth * (region.column + region.columnSpan) / region.columns), bottom = Math.floor(sourceHeight * (region.row + region.rowSpan) / region.rows);
  const cropWidth = right - x, cropHeight = bottom - y; if (cropWidth < 1 || cropHeight < 1) throw Error('Capture region has no source pixels.');
  const resize = options.resize; let width, height;
  if (resize) {
    if (resize.width != null && resize.height != null) [width, height] = [resize.width, resize.height];
    else if (resize.width != null) [width, height] = [resize.width, Math.max(1, Math.round(cropHeight * resize.width / cropWidth))];
    else if (resize.height != null) [width, height] = [Math.max(1, Math.round(cropWidth * resize.height / cropHeight)), resize.height];
    else [width, height] = [cropWidth, cropHeight];
  } else { const ratio = Math.min(1, options.maxWidth / cropWidth, options.maxHeight / cropHeight); [width, height] = [Math.max(1, Math.floor(cropWidth * ratio)), Math.max(1, Math.floor(cropHeight * ratio))]; }
  if (width > 8192 || height > 8192 || width * height > 24000000) throw Error('Capture output exceeds safe bounds.');
  const filter = resize?.filter ?? 1;
  return { x, y, cropWidth, cropHeight, width, height, smoothingEnabled: filter !== 0, smoothingQuality: filter === 2 ? 'high' : 'medium' };
}

async function processScreenshot(data, sourceWidth, sourceHeight, options) {
  const bitmap = await createImageBitmap(await (await fetch(`data:image/png;base64,${data}`)).blob());
  try {
    // Screenshot pixels are authoritative for crop boundaries (they may differ from CSS units under device scale).
    sourceWidth = bitmap.width; sourceHeight = bitmap.height;
    const transform = resolveCaptureTransform(sourceWidth, sourceHeight, options);
    const canvas = new OffscreenCanvas(transform.width, transform.height), context = canvas.getContext('2d');
    context.imageSmoothingEnabled = transform.smoothingEnabled;
    context.imageSmoothingQuality = transform.smoothingQuality;
    context.drawImage(bitmap, transform.x, transform.y, transform.cropWidth, transform.cropHeight, 0, 0, transform.width, transform.height);
    const blob = await canvas.convertToBlob({ type: 'image/jpeg', quality: 0.7 });
    return { data: arrayToBase64(new Uint8Array(await blob.arrayBuffer())), width: transform.width, height: transform.height };
  } finally { bitmap.close(); }
}
function arrayToBase64(bytes) { let result = ''; for (const value of bytes) result += String.fromCharCode(value); return btoa(result); }
