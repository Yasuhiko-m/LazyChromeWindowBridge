// Product runtime entry point; no page access or caller transport is implemented.
chrome.runtime.onInstalled.addListener(() => {
  console.info("LazyChromeExtension scaffold installed.");
});
