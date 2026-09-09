using System.Text.Json.Serialization;

namespace LazyChromeWindowBridge.Core;

[JsonConverter(typeof(JsonStringEnumConverter<DownloadLifecycleState>))]
public enum DownloadLifecycleState { Created, Complete, Interrupted }

/// <summary>Profile-global Chrome state; Filename may contain a sensitive absolute path.</summary>
public sealed record DownloadLifecycleEvent(int DownloadId, DownloadLifecycleState State,
    string Filename, string? Error, DateTimeOffset ObservedAt);
