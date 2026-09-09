using System.Diagnostics;
using Microsoft.Win32;

namespace LazyChromeWindowBridge.Core;

internal static class ChromeLauncher
{
    public static void Launch(BridgeOptions options, Uri bootstrap)
    {
        var start = new ProcessStartInfo(options.Executable) { UseShellExecute = false };
        if (options.UserDataDirectory is not null) start.ArgumentList.Add("--user-data-dir=" + options.UserDataDirectory);
        start.ArgumentList.Add("--no-first-run");
        start.ArgumentList.Add("--no-default-browser-check");
        start.ArgumentList.Add("--new-window");
        start.ArgumentList.Add(bootstrap.AbsoluteUri);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Chrome did not start.");
    }
}
