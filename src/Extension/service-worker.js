import { BindingManager } from "./bindings.js";
import { MonitorManager } from "./monitor.js";

const monitors = new MonitorManager(chrome);
const manager = new BindingManager(chrome, globalThis.fetch.bind(globalThis), monitors);
let monitorRecovered = false;
let queue = Promise.resolve();
function enqueue(action) {
  const operation = queue.then(action);
  queue = operation.catch(() => {});
  return operation;
}
async function recover() {
  await chrome.storage.session.setAccessLevel({ accessLevel: "TRUSTED_CONTEXTS" });
  if (!monitorRecovered) { await monitors.recover(); monitorRecovered = true; }
  await chrome.alarms.create("session-reconcile", { periodInMinutes: 0.5 });
  await manager.reconcile();
}
chrome.runtime.onMessage.addListener((message, sender, sendResponse) => {
  if (message?.type !== "bind-bootstrap") return;
  enqueue(() => manager.bootstrap(message, sender))
    .then(() => sendResponse({ ok: true }), error => sendResponse({ ok: false, error: error.message }));
  return true;
});
chrome.windows.onRemoved.addListener(windowId => { void enqueue(() => manager.windowRemoved(windowId)); });
chrome.alarms.onAlarm.addListener(alarm => {
  if (alarm.name === "session-reconcile") void enqueue(() => manager.reconcile());
});
chrome.runtime.onInstalled.addListener(() => { void enqueue(recover); });
chrome.runtime.onStartup.addListener(() => { void enqueue(recover); });
// Every new worker instance rehydrates from storage, including normal idle suspension/restart.
void enqueue(recover);
