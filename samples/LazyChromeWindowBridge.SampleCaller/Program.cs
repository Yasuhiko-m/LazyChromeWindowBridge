using LazyChromeWindowBridge.Core;

namespace LazyChromeWindowBridge.SampleCaller;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        try { Application.Run(new SampleCallerForm(BridgeOptions.Parse(args))); }
        catch (Exception error) { MessageBox.Show(error.Message, "LazyChromeWindowBridge", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }
}
