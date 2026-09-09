namespace CallerHarness;

internal sealed class MainForm : Form
{
    private readonly TextBox launchUrl = new() { Text = "https://chatgpt.com/", Dock = DockStyle.Fill, AccessibleName = "Launch URL" };
    private readonly Button launchButton = new() { Text = "&Launch", AutoSize = true, Enabled = false };
    private readonly Label status = new() { Text = "Status: Starting caller", AutoSize = true, Dock = DockStyle.Fill };
    private readonly DataGridView sessions = new()
    {
        Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
        AutoGenerateColumns = false, RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        AccessibleName = "Session bindings"
    };
    private readonly System.Windows.Forms.Timer refresh = new() { Interval = 500 };
    private SessionHost? host;
    private bool shutdownComplete;
    private bool closing;

    public MainForm(ChromeOptions chrome)
    {
        Text = "LazyChromeExtension Caller Harness";
        ClientSize = new Size(920, 350);
        MinimumSize = new Size(700, 300);
        AutoScaleMode = AutoScaleMode.Dpi;
        StartPosition = FormStartPosition.CenterScreen;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            ColumnCount = 2,
            RowCount = 5
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var urlLabel = new Label
        {
            Text = "Launch &URL:",
            AutoSize = true,
            Anchor = AnchorStyles.Left
        };
        launchButton.Margin = new Padding(3, 10, 3, 10);
        launchButton.TabIndex = 1;

        layout.Controls.Add(urlLabel, 0, 0);
        layout.Controls.Add(launchUrl, 1, 0);
        layout.Controls.Add(launchButton, 1, 1);
        layout.Controls.Add(status, 0, 2);
        layout.SetColumnSpan(status, 2);
        var note = new Label { Text = "Each Launch opens a new session window. Closing this caller leaves Chrome windows open and stops tracking.", AutoSize = true, Margin = new Padding(3, 8, 3, 8) };
        layout.Controls.Add(note, 0, 3);
        layout.SetColumnSpan(note, 2);
        foreach (var (name, width) in new[] { ("Session", 270), ("Window", 105), ("State", 95), ("Launch URL", 360) })
            sessions.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = name, Width = width, SortMode = DataGridViewColumnSortMode.NotSortable });
        layout.Controls.Add(sessions, 0, 4);
        layout.SetColumnSpan(sessions, 2);
        Controls.Add(layout);
        AcceptButton = launchButton;
        Shown += async (_, _) =>
        {
            try
            {
                var started = await SessionHost.StartAsync(chrome);
                if (closing) { await started.DisposeAsync(); return; }
                host = started;
                status.Text = "Status: Ready";
                launchButton.Enabled = true;
                refresh.Start();
            }
            catch (Exception error) { if (!closing) status.Text = "Status: Caller start failed: " + error.Message; }
        };
        launchButton.Click += async (_, _) =>
        {
            if (host is null) return;
            launchButton.Enabled = false;
            try
            {
                var session = await host.LaunchAsync(launchUrl.Text.Trim());
                if (!closing) { status.Text = $"Status: {session.State} — {session.AppSessionId}"; RefreshSessions(); }
            }
            catch (Exception error) { if (!closing) status.Text = "Status: " + error.Message; }
            finally { if (!closing) launchButton.Enabled = true; }
        };
        refresh.Tick += (_, _) => RefreshSessions();
        FormClosing += async (_, args) =>
        {
            if (shutdownComplete) return;
            args.Cancel = true;
            if (closing) return;
            closing = true;
            launchButton.Enabled = false;
            refresh.Stop();
            if (host is not null) await host.DisposeAsync();
            shutdownComplete = true;
            Close();
        };
        FormClosed += (_, _) => refresh.Dispose();
    }

    private void RefreshSessions()
    {
        if (host is null) return;
        var current = host.Sessions.GetAll();
        for (var i = 0; i < current.Length; i++)
        {
            if (sessions.Rows.Count <= i) sessions.Rows.Add();
            var session = current[i];
            sessions.Rows[i].SetValues(session.AppSessionId, session.WindowId?.ToString() ?? "—", session.State, session.LaunchUrl);
            sessions.Rows[i].Cells[2].ToolTipText = session.Detail;
        }
        while (sessions.Rows.Count > current.Length) sessions.Rows.RemoveAt(sessions.Rows.Count - 1);
        if (current.LastOrDefault() is { } latest) status.Text = $"Status: {latest.State} — {latest.Detail}";
    }
}
