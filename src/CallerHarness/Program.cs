namespace CallerHarness;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        try { Application.Run(new MainForm(ChromeOptions.Parse(args))); }
        catch (Exception error) { MessageBox.Show(error.Message, "LazyChromeExtension", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }
}
