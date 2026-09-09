using System.Security.Cryptography;
using System.Text;

namespace CallerHarness;

internal enum SessionState { Launching, Bound, Disconnected, Closed, Failed }
internal sealed record SessionSnapshot(Guid AppSessionId, string LaunchUrl, int? WindowId, int? TabId,
    string? BrowserSessionId, SessionState State, string Detail);
internal sealed record WindowReport(string BrowserSessionId, int WindowId, int TabId);

internal sealed class SessionRegistry
{
    private sealed class Entry(Guid id, string url, string token, DateTimeOffset now)
    {
        public Guid Id { get; } = id;
        public string Url { get; } = url;
        public string Token { get; } = token;
        public WindowReport? Window { get; set; }
        public SessionState State { get; set; } = SessionState.Launching;
        public DateTimeOffset LastSeen { get; set; } = now;
        public string Detail { get; set; } = "Waiting for the installed extension (30-second timeout).";
        public SessionSnapshot Snapshot() => new(Id, Url, Window?.WindowId, Window?.TabId,
            Window?.BrowserSessionId, State, Detail);
    }
    private readonly object gate = new();
    private readonly Dictionary<Guid, Entry> entries = [];

    public static string ValidateLaunchUrl(string value)
    {
        if (value.Length > 4096 || !Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            (uri.Scheme != "http" && uri.Scheme != "https") || string.IsNullOrEmpty(uri.Host) || !string.IsNullOrEmpty(uri.UserInfo))
            throw new ArgumentException("Enter an absolute HTTP or HTTPS URL without embedded credentials.");
        return uri.AbsoluteUri;
    }
    public (SessionSnapshot Session, string Token) Create(string url, DateTimeOffset now)
    {
        url = ValidateLaunchUrl(url);
        lock (gate)
        {
            if (entries.Count >= 256)
            {
                var terminal = entries.Values.FirstOrDefault(e => e.State is SessionState.Closed or SessionState.Failed);
                if (terminal is null) throw new InvalidOperationException("The caller already has 256 active sessions.");
                entries.Remove(terminal.Id);
            }
            var entry = new Entry(Guid.NewGuid(), url, Convert.ToHexString(RandomNumberGenerator.GetBytes(32)), now);
            entries.Add(entry.Id, entry);
            return (entry.Snapshot(), entry.Token);
        }
    }
    public bool Authenticate(Guid id, string supplied)
    {
        lock (gate)
            return entries.TryGetValue(id, out var entry) && supplied.Length == entry.Token.Length &&
                CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(supplied), Encoding.ASCII.GetBytes(entry.Token));
    }
    public SessionSnapshot? Get(Guid id) { lock (gate) return entries.GetValueOrDefault(id)?.Snapshot(); }
    public SessionSnapshot[] GetAll() { lock (gate) return entries.Values.Select(e => e.Snapshot()).ToArray(); }

    public bool Report(Guid id, WindowReport report, bool closed, DateTimeOffset now)
    {
        if (!Guid.TryParseExact(report.BrowserSessionId, "D", out _) || report.WindowId < 0 || report.TabId < 0) return false;
        lock (gate)
        {
            if (!entries.TryGetValue(id, out var entry) || entry.State == SessionState.Failed) return false;
            if (entry.Window is not null && entry.Window != report) return false;
            if (entry.State == SessionState.Closed) return closed && entry.Window == report;
            if (entries.Values.Any(other => other.Id != id && other.State is not (SessionState.Closed or SessionState.Failed) &&
                    other.Window?.BrowserSessionId == report.BrowserSessionId && other.Window.WindowId == report.WindowId)) return false;
            entry.Window = report;
            entry.State = closed ? SessionState.Closed : SessionState.Bound;
            entry.Detail = closed ? "The bound Chrome window closed." : "Session owns this window; navigation does not change ownership.";
            entry.LastSeen = now;
            return true;
        }
    }
    public void FailLaunch(Guid id, string detail)
    {
        lock (gate)
        {
            if (entries.TryGetValue(id, out var entry) && entry.State == SessionState.Launching)
            { entry.State = SessionState.Failed; entry.Detail = detail; }
        }
    }
    public void Sweep(DateTimeOffset now)
    {
        lock (gate)
        {
            foreach (var entry in entries.Values)
            {
                if (entry.State == SessionState.Launching && now - entry.LastSeen > TimeSpan.FromSeconds(30))
                {
                    entry.State = SessionState.Failed;
                    entry.Detail = "No binding received. Check that LazyChromeExtension is enabled in the selected Chrome profile.";
                }
                else if (entry.State == SessionState.Bound && now - entry.LastSeen > TimeSpan.FromSeconds(100))
                {
                    entry.State = SessionState.Disconnected;
                    entry.Detail = "Extension contact lost; ownership retained, never reassigned. Browser restart recovery is not supported.";
                }
            }
        }
    }
}
