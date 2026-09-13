using System.Diagnostics;

namespace LazyChromeWindowBridge.Core;

internal static class ChromeLauncher
{
    public static void Launch(BridgeOptions options, Uri bootstrap)
    {
        using var process = Process.Start(CreateStartInfo(options, bootstrap)) ?? throw new InvalidOperationException("Chrome did not start.");
    }
    internal static ProcessStartInfo CreateStartInfo(BridgeOptions options, Uri bootstrap)
    {
        var start = new ProcessStartInfo(options.Executable) { UseShellExecute = false };
        if (options.UserDataDirectory is not null) start.ArgumentList.Add("--user-data-dir=" + options.UserDataDirectory);
        start.ArgumentList.Add("--no-first-run");
        start.ArgumentList.Add("--no-default-browser-check");
        // Best-effort Chrome UX only; debugger permissions/command boundaries still apply.
        start.ArgumentList.Add("--silent-debugger-extension-api");
        start.ArgumentList.Add("--new-window");
        start.ArgumentList.Add(bootstrap.AbsoluteUri);
        return start;
    }
}
