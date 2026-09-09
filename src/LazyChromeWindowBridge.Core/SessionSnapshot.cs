namespace LazyChromeWindowBridge.Core;

public enum SessionState { Launching, Bound, Disconnected, Closed, Failed }
public sealed record SessionSnapshot(Guid AppSessionId, string LaunchUrl, int? WindowId, int? TabId,
    string? BrowserSessionId, SessionState State, string Detail);
