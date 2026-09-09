// This runs only on LazyChromeWindowBridge.Core's local bootstrap path. No DOM or page content is read.
(() => {
  const url = new URL(location.href);
  if (window !== window.top || url.hostname !== "127.0.0.1" ||
      url.pathname !== "/lazy-chrome-window-bridge/bootstrap") return;
  let attempts = 0;
  async function connect() {
    try {
      const result = await chrome.runtime.sendMessage({ type: "bind-bootstrap", url: url.href });
      if (result?.ok) return;
    } catch { /* The worker may be starting; retry only this bounded bootstrap. */ }
    if (++attempts < 10 && location.href === url.href) setTimeout(connect, 1000);
  }
  void connect();
})();
