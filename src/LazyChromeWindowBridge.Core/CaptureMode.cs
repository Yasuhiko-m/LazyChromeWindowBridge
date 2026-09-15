namespace LazyChromeWindowBridge.Core;

/// <summary>Selects the source used for human-only monitor previews.</summary>
public enum CaptureMode
{
    /// <summary>Captures the active tab viewport through the existing Chrome DevTools Protocol path.</summary>
    BrowserViewport = 0,
    /// <summary>Captures the exact owned native Chrome window, including any captured non-client area.</summary>
    NativeWindow = 1
}
