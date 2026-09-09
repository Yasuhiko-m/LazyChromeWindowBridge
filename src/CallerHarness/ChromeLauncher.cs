using System.Diagnostics;
using Microsoft.Win32;

namespace CallerHarness;

internal sealed record ChromeOptions(string Executable, string? UserDataDirectory)
{
    public static ChromeOptions Parse(string[] args)
    {
        string? executable = null, profile = null;
        for (var i = 0; i < args.Length; i++)
        {
            if (i + 1 >= args.Length) throw new ArgumentException("Expected a value after the Chrome option.");
            switch (args[i])
            {
                case "--chrome-executable": executable = args[++i]; break;
                case "--chrome-user-data-dir": profile = args[++i]; break;
                default: throw new ArgumentException($"Unknown option: {args[i]}");
            }
        }
        executable = Path.GetFullPath(executable ?? FindInstalledChrome());
        if (!File.Exists(executable) || !string.Equals(Path.GetFileName(executable), "chrome.exe", StringComparison.OrdinalIgnoreCase) ||
            !((FileVersionInfo.GetVersionInfo(executable).ProductName ?? "").Contains("Chrome", StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Select an existing Google Chrome or Chrome for Testing chrome.exe.");
        return new ChromeOptions(executable, profile is null ? null : Path.GetFullPath(profile));
    }
    private static string FindInstalledChrome()
    {
        const string appPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\chrome.exe";
        var candidates = new[]
        {
            Registry.GetValue(@"HKEY_CURRENT_USER\" + appPath, "", null) as string,
            Registry.GetValue(@"HKEY_LOCAL_MACHINE\" + appPath, "", null) as string,
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Google", "Chrome", "Application", "chrome.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Google", "Chrome", "Application", "chrome.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Google", "Chrome", "Application", "chrome.exe")
        };
        return candidates.FirstOrDefault(p => p is not null && File.Exists(p)) ??
            throw new FileNotFoundException("Google Chrome was not found. Select Chrome with --chrome-executable.");
    }
}
internal static class ChromeLauncher
{
    public static void Launch(ChromeOptions options, Uri bootstrap)
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
