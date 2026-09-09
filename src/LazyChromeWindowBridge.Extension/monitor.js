// Human-view-only pixel transport. No DOM/Runtime/Network commands or image analysis.
const TARGET = 'monitor-target:';
export class MonitorManager {
  constructor(browser, Socket = globalThis.WebSocket) {
    this.browser = browser; this.Socket = Socket; this.connections = new Map();
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
    const client = { record, socket, tabId: null, control: null, running: false, removed: false, blocked: null, detaching: false,
      send: message => { if (socket.readyState === 1) socket.send(JSON.stringify(message)); } };
    this.connections.set(record.appSessionId, client);
    socket.onopen = () => client.send({ appSessionId: record.appSessionId, token: record.token });
    socket.onmessage = event => {
      try {
        const control = JSON.parse(event.data), settings = control.options;
        if (!Number.isSafeInteger(control.generation) || typeof control.enabled !== 'boolean' ||
            !Number.isInteger(settings.framesPerSecond) || settings.framesPerSecond < 1 || settings.framesPerSecond > 10 ||
            !Number.isInteger(settings.maxWidth) || settings.maxWidth < 160 || settings.maxWidth > 1920 ||
            !Number.isInteger(settings.maxHeight) || settings.maxHeight < 90 || settings.maxHeight > 1080) throw Error('Invalid monitor control.');
        if (client.control?.generation !== control.generation || client.control?.enabled !== control.enabled || control.closing) client.wake?.();
        client.control = control;
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
        const scale = Math.min(1, control.options.maxWidth / viewport.clientWidth, control.options.maxHeight / viewport.clientHeight);
        const capture = this.browser.debugger.sendCommand({ tabId }, 'Page.captureScreenshot', {
          format: 'jpeg', quality: 70, captureBeyondViewport: false,
          clip: { x: viewport.pageX, y: viewport.pageY, width: viewport.clientWidth, height: viewport.clientHeight, scale }
        });
        let timeout;
        const frame = await Promise.race([capture, new Promise((_, reject) => { timeout = setTimeout(() => reject(Error('Monitor capture timed out.')), 5000); })]).finally(() => clearTimeout(timeout));
        const current = await this.browser.tabs.get(tabId);
        if (client.removed || !client.control.enabled || client.blocked === control.generation || control.generation !== client.control.generation) break;
        if (current.windowId !== client.record.windowId || !current.active) { await this.detach(client); continue; }
        if (frame.data.length > 2800000) throw Error('Monitor frame exceeds transport bounds.');
        if (client.socket.bufferedAmount < 1000000) client.send({ type: 'frame', generation: control.generation,
          windowId: client.record.windowId, tabId, identity: control.identity, data: frame.data, captureMilliseconds: performance.now() - started });
        await new Promise(resolve => {
          const timer = setTimeout(resolve, Math.max(0, 1000 / control.options.framesPerSecond - (performance.now() - started)));
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
      if (client.closedTabId != null && !client.removed && client.control?.enabled && client.blocked !== client.control.generation) {
        client.closedTabId = null;
        void this.pump(client);
      }
    }
  }
}
