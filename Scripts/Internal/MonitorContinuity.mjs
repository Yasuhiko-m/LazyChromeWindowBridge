// Test-only observation in the existing isolated CfT extension context. No tokens,
// URLs or JPEG payloads are retained; wrappers forward the original calls unchanged.
export async function observeMonitor(cdp) {
  await cdp.extension(`(() => {
    if (globalThis.__monitorAudit) return;
    const audit = globalThis.__monitorAudit = { tabs: {}, windows: {}, sockets: 0 };
    const tab = id => audit.tabs[id] ??= { attaches: 0, detaches: 0, captures: 0 };
    for (const name of ['attach', 'detach']) {
      const original = chrome.debugger[name].bind(chrome.debugger);
      chrome.debugger[name] = (...args) => {
        tab(args[0].tabId)[name === 'attach' ? 'attaches' : 'detaches']++;
        return original(...args);
      };
    }
    const command = chrome.debugger.sendCommand.bind(chrome.debugger);
    chrome.debugger.sendCommand = async (...args) => {
      const row = tab(args[0].tabId);
      if (args[1] === 'Page.captureScreenshot') { row.captures++; row.parameters = args[2]; }
      const result = await command(...args);
      if (args[1] === 'Page.getLayoutMetrics') {
        const v = result.cssVisualViewport;
        row.viewport = { width: v.clientWidth, height: v.clientHeight };
      }
      return result;
    };
    const send = WebSocket.prototype.send, ids = new WeakMap();
    WebSocket.prototype.send = function(data) {
      const message = JSON.parse(data);
      if (message.type === 'frame') {
        if (!ids.has(this)) ids.set(this, ++audit.sockets);
        audit.windows[message.windowId] = { socket: ids.get(this), generation: message.generation, tabId: message.tabId };
      }
      return send.call(this, data);
    };
  })()`);
  return () => cdp.extension('globalThis.__monitorAudit');
}
