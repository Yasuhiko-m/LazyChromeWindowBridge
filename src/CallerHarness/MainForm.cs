namespace CallerHarness;

internal sealed class MainForm : Form
{
    private readonly TextBox launchUrl = new() { Text = "https://chatgpt.com/", Dock = DockStyle.Fill, AccessibleName = "Launch URL" };
    private readonly Button launchButton = new() { Text = "&Launch", AutoSize = true, Enabled = false };
    private readonly Button parkButton = new() { Text = "&Park selected", AutoSize = true, Enabled = false };
    private readonly Button restoreButton = new() { Text = "&Restore selected", AutoSize = true, Enabled = false };
    private readonly Label selected = new() { Text = "Select a session to control its window.", AutoSize = true };
    private bool operating;
    private Guid? pendingLaunchStatus;
    private readonly Button monitorStart = new() { Text = "Start monitor", AutoSize = true, Enabled = false };
    private readonly Button monitorStop = new() { Text = "Stop monitor", AutoSize = true, Enabled = false };
    private readonly Label monitorStatus = new() { Text = "Monitor stopped. Human view only; Chrome debugger permission/notice applies.", AutoSize = true, Dock = DockStyle.Top };
    private readonly FlowLayoutPanel thumbnails = new() { Dock = DockStyle.Fill, AutoScroll = true, WrapContents = true, AccessibleName = "Session monitor overview" };
    private readonly Dictionary<Guid, MonitorTile> tiles = [];
    private bool monitoring;
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
        ClientSize = new Size(1080, 780);
        MinimumSize = new Size(850, 600);
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
        var actions = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
        actions.Controls.AddRange([launchButton, parkButton, restoreButton, monitorStart, monitorStop, selected]);
        layout.Controls.Add(actions, 0, 1);
        layout.SetColumnSpan(actions, 2);
        layout.Controls.Add(status, 0, 2);
        layout.SetColumnSpan(status, 2);
        var note = new Label { Text = "Placement follows the original launch URL. Caller exit restores parked windows and leaves Chrome open.", AutoSize = true, Margin = new Padding(3, 8, 3, 8) };
        layout.Controls.Add(note, 0, 3);
        layout.SetColumnSpan(note, 2);
        foreach (var (name, width) in new[] { ("Session", 270), ("Window", 105), ("State", 155), ("Launch URL", 450) })
            sessions.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = name, Width = width, SortMode = DataGridViewColumnSortMode.NotSortable });
        var monitorPanel = new Panel { Dock = DockStyle.Fill };
        monitorPanel.Controls.Add(thumbnails); monitorPanel.Controls.Add(monitorStatus);
        thumbnails.SizeChanged += (_, _) => SizeTiles();
        var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, Size = new Size(1048, 600), SplitterDistance = 160, FixedPanel = FixedPanel.Panel1 };
        split.Panel1.Controls.Add(sessions); split.Panel2.Controls.Add(monitorPanel);
        layout.Controls.Add(split, 0, 4);
        layout.SetColumnSpan(split, 2);
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
                if (!closing) { pendingLaunchStatus = session.AppSessionId; status.Text = $"Status: {session.State} — {session.AppSessionId}"; RefreshSessions(); }
            }
            catch (Exception error) { if (!closing) status.Text = "Status: " + error.Message; }
            finally { if (!closing) launchButton.Enabled = true; }
        };
        refresh.Tick += (_, _) => RefreshSessions();
        sessions.SelectionChanged += (_, _) => RefreshSelection();
        parkButton.Click += async (_, _) => await Operate(true);
        restoreButton.Click += async (_, _) => await Operate(false);
        monitorStart.Click += (_, _) =>
        {
            if (host is null) return;
            try { host.Monitor.Start(new()); monitoring = true; RefreshMonitor(); }
            catch (Exception error) { monitorStatus.Text = "Monitor: " + error.Message; }
        };
        monitorStop.Click += (_, _) => { monitoring = false; host?.Monitor.Stop(); RefreshMonitor(); };
        FormClosing += async (_, args) =>
        {
            if (shutdownComplete) return;
            args.Cancel = true;
            if (closing) return;
            closing = true;
            launchButton.Enabled = false;
            parkButton.Enabled = restoreButton.Enabled = false;
            monitorStart.Enabled = monitorStop.Enabled = false;
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
            var geometry = host.Geometry.Get(session.AppSessionId);
            var placement = session.State == SessionState.Bound ? $"Bound / {geometry?.State.ToString() ?? "Mapping"}" : session.State.ToString();
            sessions.Rows[i].Tag = session.AppSessionId;
            sessions.Rows[i].SetValues(session.AppSessionId, session.WindowId?.ToString() ?? "—", placement, session.LaunchUrl);
            sessions.Rows[i].Cells[2].ToolTipText = geometry?.Error ?? session.Detail;
        }
        while (sessions.Rows.Count > current.Length) sessions.Rows.RemoveAt(sessions.Rows.Count - 1);
        if (pendingLaunchStatus is { } pending && host.Sessions.Get(pending) is { } launched && launched.State != SessionState.Launching)
        {
            status.Text = $"Status: {launched.State} — {launched.AppSessionId}";
            pendingLaunchStatus = null;
        }
        RefreshSelection();
        RefreshMonitor();
    }
    private Guid? SelectedId => sessions.CurrentRow?.Tag is Guid id ? id : null;
    private void RefreshSelection()
    {
        var id = SelectedId;
        var session = id is null ? null : host?.Sessions.Get(id.Value);
        var geometry = id is null ? null : host?.Geometry.Get(id.Value);
        selected.Text = session is null ? "Select a session." : $"Window {session.WindowId} · {session.AppSessionId.ToString()[..8]}";
        var active = !closing && !operating && session?.State == SessionState.Bound && geometry is not null && geometry.State != PlacementState.Closed;
        parkButton.Enabled = active && geometry?.State == PlacementState.Visible;
        restoreButton.Enabled = active && geometry?.State != PlacementState.Visible;
        monitorStart.Enabled = !closing && host is not null;
        monitorStop.Enabled = !closing && monitoring;
    }
    private void SizeTiles()
    {
        var width = Math.Clamp((thumbnails.ClientSize.Width - 24) / Math.Clamp(tiles.Count, 1, 5) - 8, 200, 300);
        foreach (var tile in tiles.Values) tile.Size = new Size(width, width * 9 / 16 + 68);
    }
    private void RefreshMonitor()
    {
        if (host is null || closing) return;
        var snapshot = host.Monitor.Snapshot();
        foreach (var session in snapshot.Sessions)
        {
            if (!tiles.TryGetValue(session.AppSessionId, out var tile))
            {
                tiles.Add(session.AppSessionId, tile = new MonitorTile());
                thumbnails.Controls.Add(tile); SizeTiles();
            }
            tile.Update(session, host.Monitor.Latest(session.AppSessionId));
        }
        foreach (var id in tiles.Keys.Where(id => !snapshot.Sessions.Any(s => s.AppSessionId == id)).ToArray())
        {
            var tile = tiles[id]; thumbnails.Controls.Remove(tile); tile.Dispose(); tiles.Remove(id); SizeTiles();
        }
        monitorStatus.Text = $"Monitoring {(snapshot.Enabled ? "enabled" : "stopped")} · {snapshot.CapturingConnections} PARKED captures · Visible = ACTIVE · human view only";
        monitorStart.Enabled = true; monitorStop.Enabled = monitoring;
    }
    private async Task Operate(bool park)
    {
        if (host is null || SelectedId is not { } id) return;
        pendingLaunchStatus = null;
        operating = true;
        refresh.Stop();
        RefreshSelection();
        try
        {
            var result = await Task.Run(() => park ? host.Geometry.Park(id) : host.Geometry.Restore(id));
            if (!closing) status.Text = $"Status: Window {result.WindowId} / {result.State} — {result.AppSessionId}";
        }
        catch (Exception error) { if (!closing) status.Text = "Status: " + error.Message; }
        finally { operating = false; if (!closing) { RefreshSessions(); refresh.Start(); } }
    }
}
