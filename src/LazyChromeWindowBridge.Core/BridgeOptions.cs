using System.Diagnostics;
using Microsoft.Win32;

namespace LazyChromeWindowBridge.Core;

public sealed record BridgeOptions(string Executable, string? UserDataDirectory, string? GeometryDirectory = null)
{
    public static BridgeOptions Parse(string[] args)
    {
        string? executable = null, profile = null, geometry = null;
        for (var i = 0; i < args.Length; i++)
        {
            if (i + 1 >= args.Length) throw new ArgumentException("Expected a value after the Chrome option.");
            switch (args[i])
            {
                case "--chrome-executable": executable = args[++i]; break;
                case "--chrome-user-data-dir": profile = args[++i]; break;
                case "--geometry-directory": geometry = args[++i]; break;
                default: throw new ArgumentException($"Unknown option: {args[i]}");
            }
        }
        executable = Path.GetFullPath(executable ?? FindInstalledChrome());
        if (!File.Exists(executable) || !string.Equals(Path.GetFileName(executable), "chrome.exe", StringComparison.OrdinalIgnoreCase) ||
            !((FileVersionInfo.GetVersionInfo(executable).ProductName ?? "").Contains("Chrome", StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Select an existing Google Chrome or Chrome for Testing chrome.exe.");
        return new BridgeOptions(executable, profile is null ? null : Path.GetFullPath(profile), geometry is null ? null : Path.GetFullPath(geometry));
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
