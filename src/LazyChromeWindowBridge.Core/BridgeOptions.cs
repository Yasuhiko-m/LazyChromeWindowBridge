using System.Diagnostics;
using Microsoft.Win32;

namespace LazyChromeWindowBridge.Core;

public sealed record BridgeOptions(string Executable, string? UserDataDirectory, string? GeometryDirectory = null)
{
    private static readonly string[] ReservedChromeArguments =
    ["--user-data-dir", "--new-window", "--no-first-run", "--no-default-browser-check",
        "--silent-debugger-extension-api", "--disable-backgrounding-occluded-windows"];

    public bool PreserveBackgroundRendering { get; init; } = true;
    public IReadOnlyList<string> AdditionalChromeArguments { get; init; } = Array.Empty<string>();

    public static BridgeOptions Parse(string[] args)
    {
        var parsed = ParseCommandLine(args);
        var executable = parsed.Executable;
        var profile = parsed.Profile;
        var geometry = parsed.Geometry;
        executable = Path.GetFullPath(executable ?? FindInstalledChrome());
        if (!File.Exists(executable) || !string.Equals(Path.GetFileName(executable), "chrome.exe", StringComparison.OrdinalIgnoreCase) ||
            !((FileVersionInfo.GetVersionInfo(executable).ProductName ?? "").Contains("Chrome", StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Select an existing Google Chrome or Chrome for Testing chrome.exe.");
        return new BridgeOptions(executable, profile is null ? null : Path.GetFullPath(profile), geometry is null ? null : Path.GetFullPath(geometry))
        {
            AdditionalChromeArguments = parsed.AdditionalChromeArguments
        };
    }
    internal static (string? Executable, string? Profile, string? Geometry, IReadOnlyList<string> AdditionalChromeArguments) ParseCommandLine(string[] args)
    {
        string? executable = null, profile = null, geometry = null;
        var additional = new List<string>();
        for (var i = 0; i < args.Length; i++)
        {
            if (i + 1 >= args.Length) throw new ArgumentException("Expected a value after the Chrome option.");
            switch (args[i])
            {
                case "--chrome-executable": executable = args[++i]; break;
                case "--chrome-user-data-dir": profile = args[++i]; break;
                case "--geometry-directory": geometry = args[++i]; break;
                case "--chrome-argument":
                    var argument = args[++i];
                    ValidateAdditionalChromeArgument(argument);
                    additional.Add(argument);
                    break;
                default: throw new ArgumentException($"Unknown option: {args[i]}");
            }
        }
        return (executable, profile, geometry, additional.ToArray());
    }
    internal IReadOnlyList<string> ValidatedAdditionalChromeArguments()
    {
        if (AdditionalChromeArguments is null) throw new ArgumentException("Additional Chrome arguments cannot be null.");
        foreach (var argument in AdditionalChromeArguments) ValidateAdditionalChromeArgument(argument);
        return AdditionalChromeArguments;
    }
    private static void ValidateAdditionalChromeArgument(string? argument)
    {
        if (string.IsNullOrWhiteSpace(argument) || argument.Length <= 2 || !argument.StartsWith("--", StringComparison.Ordinal) ||
            argument.Any(char.IsControl))
            throw new ArgumentException("Additional Chrome arguments must be non-empty Chrome switches without control characters.");
        var equals = argument.IndexOf('=');
        var name = equals < 0 ? argument : argument[..equals];
        if (name.Length <= 2 || name.Any(char.IsWhiteSpace))
            throw new ArgumentException("Additional Chrome arguments must be single Chrome switches.");
        if (ReservedChromeArguments.Contains(name, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException($"Additional Chrome argument is managed by LazyChromeWindowBridge: {name}");
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
