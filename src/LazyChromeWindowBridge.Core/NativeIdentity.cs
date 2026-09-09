namespace LazyChromeWindowBridge.Core;

public sealed record NativeIdentity(long Hwnd, uint ProcessId, long Tag);
