using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace LazyChromeWindowBridge.Core;

internal interface INativeWindows
{
    NativeIdentity? FindAndTag(string marker, string chromeExecutable);
    bool Alive(NativeIdentity identity);
    PixelRect Read(NativeIdentity identity);
    bool Normal(NativeIdentity identity);
    void Move(NativeIdentity identity, PixelRect rectangle);
    uint Dpi(NativeIdentity identity);
    MonitorGeometry[] Monitors();
    void Release(NativeIdentity identity);
}
internal sealed class NativeWindows : INativeWindows
{
    private const string Property = "LazyChromeWindowBridge.SessionBinding";
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; public readonly PixelRect Pixels => new(Left, Top, Right - Left, Bottom - Top); }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct MonitorInfo
    { public int Size; public Rect Monitor, Work; public uint Flags; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Device; }
    private delegate bool EnumWindowCallback(nint window, nint data);
    private delegate bool EnumMonitorCallback(nint monitor, nint dc, ref Rect rect, nint data);
    [DllImport("user32.dll")] private static extern nint SetThreadDpiAwarenessContext(nint value);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowCallback callback, nint data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(nint window, StringBuilder text, int count);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(nint window, StringBuilder text, int count);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out uint process);
    [DllImport("user32.dll")] private static extern bool IsWindow(nint window);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint window);
    [DllImport("user32.dll")] private static extern bool IsIconic(nint window);
    [DllImport("user32.dll")] private static extern bool IsZoomed(nint window);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool GetWindowRect(nint window, out Rect rect);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool SetWindowPos(nint window, nint after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] private static extern bool ShowWindowAsync(nint window, int command);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool SetProp(nint window, string name, nint value);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern nint GetProp(nint window, string name);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern nint RemoveProp(nint window, string name);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(nint window);
    [DllImport("user32.dll")] private static extern bool EnumDisplayMonitors(nint dc, nint clip, EnumMonitorCallback callback, nint data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);
    [DllImport("shcore.dll")] private static extern int GetScaleFactorForMonitor(nint monitor, out uint scale);
    private sealed class PhysicalCoordinates : IDisposable
    {
        private readonly nint prior;
        public PhysicalCoordinates()
        {
            prior = SetThreadDpiAwarenessContext(-4); // PER_MONITOR_AWARE_V2; scoped to this synchronous native operation.
            if (prior == 0) throw new InvalidOperationException("Cannot establish physical-pixel DPI context.");
        }
        public void Dispose() { SetThreadDpiAwarenessContext(prior); }
    }
    public NativeIdentity? FindAndTag(string marker, string chromeExecutable)
    {
        using var dpi = new PhysicalCoordinates();
        var candidates = new List<(nint Hwnd, uint Pid)>();
        EnumWindows((window, _) =>
        {
            var name = new StringBuilder(256);
            GetClassName(window, name, name.Capacity);
            if (name.ToString() != "Chrome_WidgetWin_1" || !IsWindowVisible(window)) return true;
            var title = new StringBuilder(512);
            GetWindowText(window, title, title.Capacity);
            if (!title.ToString().StartsWith(marker + " - ", StringComparison.Ordinal) && title.ToString() != marker) return true;
            GetWindowThreadProcessId(window, out var pid);
            try
            {
                using var process = Process.GetProcessById((int)pid);
                if (string.Equals(Path.GetFullPath(process.MainModule!.FileName), Path.GetFullPath(chromeExecutable), StringComparison.OrdinalIgnoreCase)) candidates.Add((window, pid));
            }
            catch (Exception e) when (e is Win32Exception or InvalidOperationException or ArgumentException) { }
            return true;
        }, 0);
        if (candidates.Count != 1) return null; // Never choose first/foreground/URL-matching Chrome window.
        var selected = candidates[0];
        if (GetProp(selected.Hwnd, Property) != 0) return null;
        var tag = Random.Shared.NextInt64(1, int.MaxValue);
        if (!SetProp(selected.Hwnd, Property, (nint)tag)) throw new Win32Exception(Marshal.GetLastWin32Error());
        return new NativeIdentity((long)selected.Hwnd, selected.Pid, tag);
    }
    public bool Alive(NativeIdentity identity) => IsWindow((nint)identity.Hwnd) && GetProp((nint)identity.Hwnd, Property) == (nint)identity.Tag &&
        GetWindowThreadProcessId((nint)identity.Hwnd, out var pid) != 0 && pid == identity.ProcessId;
    private void Require(NativeIdentity identity) { if (!Alive(identity)) throw new InvalidOperationException("Native session window no longer exists; no replacement will be selected."); }
    public PixelRect Read(NativeIdentity identity)
    {
        Require(identity); using var dpi = new PhysicalCoordinates();
        if (!GetWindowRect((nint)identity.Hwnd, out var rect)) throw new Win32Exception(Marshal.GetLastWin32Error());
        return rect.Pixels;
    }
    public bool Normal(NativeIdentity identity) => Alive(identity) && !IsIconic((nint)identity.Hwnd) && !IsZoomed((nint)identity.Hwnd);
    public uint Dpi(NativeIdentity identity) { Require(identity); return GetDpiForWindow((nint)identity.Hwnd); }
    public MonitorGeometry[] Monitors()
    {
        using var dpi = new PhysicalCoordinates();
        var monitors = new List<MonitorGeometry>();
        var complete = true;
        var enumerated = EnumDisplayMonitors(0, 0, (nint monitor, nint dc, ref Rect rect, nint data) =>
        {
            var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>(), Device = "" };
            if (GetMonitorInfo(monitor, ref info))
            {
                // GetDpiForMonitor is unsuitable on a per-monitor-aware thread. Report monitor
                // scale and its nominal layout DPI; actual window DPI comes from GetDpiForWindow.
                var result = GetScaleFactorForMonitor(monitor, out var scale);
                var effectiveDpi = result == 0 ? scale * 96 / 100 : 0;
                monitors.Add(new MonitorGeometry(info.Device, info.Monitor.Pixels, info.Work.Pixels, (info.Flags & 1) != 0, effectiveDpi, effectiveDpi, result == 0 ? scale : 0));
            }
            else complete = false;
            return true;
        }, 0);
        if (!enumerated || !complete || monitors.Count == 0) throw new InvalidOperationException("Cannot enumerate all active monitor rectangles.");
        return monitors.ToArray();
    }
    public void Move(NativeIdentity identity, PixelRect rectangle)
    {
        Require(identity); if (!rectangle.Valid) throw new ArgumentException("Invalid physical rectangle.");
        using var dpi = new PhysicalCoordinates();
        if (!Normal(identity))
        {
            ShowWindowAsync((nint)identity.Hwnd, 9);
            for (var i = 0; i < 20 && !Normal(identity); i++) Thread.Sleep(50);
        }
        for (var attempt = 0; attempt < 3; attempt++)
        {
            Require(identity);
            if (!SetWindowPos((nint)identity.Hwnd, 0, rectangle.Left, rectangle.Top, rectangle.Width, rectangle.Height, 0x4000 | 0x0010 | 0x0004))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            for (var i = 0; i < 10; i++) { Thread.Sleep(40); if (Read(identity).Near(rectangle)) return; }
        }
        throw new InvalidOperationException("Windows did not apply the requested physical rectangle within 2 pixels.");
    }
    public void Release(NativeIdentity identity) { if (Alive(identity)) RemoveProp((nint)identity.Hwnd, Property); }
}
