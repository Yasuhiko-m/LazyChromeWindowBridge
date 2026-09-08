namespace CallerHarness;

internal sealed class MainForm : Form
{
    public MainForm()
    {
        Text = "LazyChromeExtension Caller Harness";
        ClientSize = new Size(640, 200);
        MinimumSize = new Size(520, 240);
        AutoScaleMode = AutoScaleMode.Dpi;
        StartPosition = FormStartPosition.CenterScreen;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            ColumnCount = 2,
            RowCount = 3
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var urlLabel = new Label
        {
            Text = "Launch &URL:",
            AutoSize = true,
            Anchor = AnchorStyles.Left
        };
        var launchUrl = new TextBox
        {
            Text = "https://chatgpt.com/",
            Dock = DockStyle.Fill,
            AccessibleName = "Launch URL",
            TabIndex = 0
        };
        var launchButton = new Button
        {
            Text = "&Launch",
            AutoSize = true,
            TabIndex = 1,
            Margin = new Padding(3, 12, 3, 12)
        };
        var status = new Label
        {
            Text = "Status: Ready",
            AutoSize = true,
            Dock = DockStyle.Fill
        };

        launchButton.Click += (_, _) =>
            status.Text = "Status: Placeholder only. Session launch is not implemented yet.";

        layout.Controls.Add(urlLabel, 0, 0);
        layout.Controls.Add(launchUrl, 1, 0);
        layout.Controls.Add(launchButton, 1, 1);
        layout.Controls.Add(status, 0, 2);
        layout.SetColumnSpan(status, 2);
        Controls.Add(layout);
        AcceptButton = launchButton;
    }
}
