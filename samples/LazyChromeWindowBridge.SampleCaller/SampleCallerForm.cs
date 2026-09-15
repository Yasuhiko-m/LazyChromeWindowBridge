using LazyChromeWindowBridge.Core;

namespace LazyChromeWindowBridge.SampleCaller;

internal sealed class SampleCallerForm : Form
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
    private readonly Button monitorPause = new() { Text = "Pause preview", AutoSize = true, Enabled = false };
    private readonly Button monitorResume = new() { Text = "Resume preview", AutoSize = true, Enabled = false };
    private readonly Button hideTaskbar = new() { Text = "Hide from taskbar", AutoSize = true, Enabled = false };
    private readonly Button showTaskbar = new() { Text = "Show in taskbar", AutoSize = true, Enabled = false };
    private readonly HashSet<Guid> taskbarHidden = [];
    private readonly ComboBox captureMode = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 145, AccessibleName = "Capture mode" };
    private readonly NumericUpDown monitorFps = new() { Minimum = 1, Maximum = 30, Value = 2, Width = 55, AccessibleName = "Monitor FPS" };
    private readonly NumericUpDown monitorWidth = new() { Minimum = 160, Maximum = 1920, Value = 240, Width = 70, AccessibleName = "JPEG max width" };
    private readonly NumericUpDown monitorHeight = new() { Minimum = 90, Maximum = 1080, Value = 135, Width = 70, AccessibleName = "JPEG max height" };
    private readonly Button monitorApply = new() { Text = "Apply preview", AutoSize = true, Enabled = false };
    private readonly Label monitorStatus = new() { Text = "Monitor stopped. BrowserViewport uses Chrome debugger; NativeWindow captures only the owned HWND.", AutoSize = true, Dock = DockStyle.Top };
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
    private readonly System.Windows.Forms.Timer previewRefresh = new() { Interval = 33 };
    private BridgeRuntime? host;
    private bool shutdownComplete;
    private bool closing;

    public SampleCallerForm(BridgeOptions chrome)
    {
        Text = "LazyChromeWindowBridge SampleCaller";
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
        captureMode.Items.AddRange(Enum.GetNames<CaptureMode>());
        captureMode.SelectedItem = CaptureMode.BrowserViewport.ToString();

        layout.Controls.Add(urlLabel, 0, 0);
        layout.Controls.Add(launchUrl, 1, 0);
        var actions = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
        actions.Controls.AddRange([launchButton, parkButton, restoreButton, hideTaskbar, showTaskbar, monitorStart, monitorStop, monitorPause, monitorResume, selected]);
        layout.Controls.Add(actions, 0, 1);
        layout.SetColumnSpan(actions, 2);
        layout.Controls.Add(status, 0, 2);
        layout.SetColumnSpan(status, 2);
        var note = new Label { Text = "Caller controls preview policy. Park/Restore never changes Monitor ON/OFF. Exit restores windows and leaves Chrome open.", AutoSize = true, Margin = new Padding(3, 8, 3, 8) };
        layout.Controls.Add(note, 0, 3);
        layout.SetColumnSpan(note, 2);
        foreach (var (name, width) in new[] { ("Session", 270), ("Window", 105), ("State", 155), ("Launch URL", 450) })
            sessions.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = name, Width = width, SortMode = DataGridViewColumnSortMode.NotSortable });
        var monitorPanel = new Panel { Dock = DockStyle.Fill };
        monitorPanel.Controls.Add(thumbnails); monitorPanel.Controls.Add(monitorStatus);
        var previewOptions = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top };
        previewOptions.Controls.AddRange([
            new Label { Text = "Capture", AutoSize = true }, captureMode,
            new Label { Text = "FPS", AutoSize = true }, monitorFps,
            new Label { Text = "JPEG max width", AutoSize = true }, monitorWidth,
            new Label { Text = "height", AutoSize = true }, monitorHeight, monitorApply,
            new Label { Text = "Requested rate · JPEG bounds only · quality 70", AutoSize = true }]);
        monitorPanel.Controls.Add(previewOptions);
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
                var started = await BridgeRuntime.StartAsync(chrome);
                if (closing) { await started.DisposeAsync(); return; }
                host = started;
                status.Text = "Status: Ready";
                launchButton.Enabled = true;
                refresh.Start();
                previewRefresh.Start();
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
        previewRefresh.Tick += (_, _) => RefreshMonitor();
        sessions.SelectionChanged += (_, _) => RefreshSelection();
        parkButton.Click += async (_, _) => await Operate(true);
        restoreButton.Click += async (_, _) => await Operate(false);
        monitorStart.Click += (_, _) =>
        {
            if (host is null) return;
            try { host.StartMonitoring(RequestedCapture()); monitoring = true; RefreshMonitor(); }
            catch (Exception error) { monitorStatus.Text = "Monitor: " + error.Message; }
        };
        monitorApply.Click += (_, _) =>
        {
            if (host is null || !monitoring) return;
            try { host.StartMonitoring(RequestedCapture()); RefreshMonitor(); }
            catch (Exception error) { monitorStatus.Text = "Monitor: " + error.Message; }
        };
        monitorStop.Click += (_, _) => { monitoring = false; host?.StopMonitoring(); RefreshMonitor(); };
        monitorPause.Click += (_, _) => SetSelectedMonitoring(false);
        monitorResume.Click += (_, _) => SetSelectedMonitoring(true);
        hideTaskbar.Click += (_, _) => SetSelectedTaskbar(false);
        showTaskbar.Click += (_, _) => SetSelectedTaskbar(true);
        FormClosing += async (_, args) =>
        {
            if (shutdownComplete) return;
            args.Cancel = true;
            if (closing) return;
            closing = true;
            launchButton.Enabled = false;
            parkButton.Enabled = restoreButton.Enabled = false;
            hideTaskbar.Enabled = showTaskbar.Enabled = false;
            monitorStart.Enabled = monitorStop.Enabled = monitorApply.Enabled = false;
            monitorPause.Enabled = monitorResume.Enabled = false;
            refresh.Stop();
            previewRefresh.Stop();
            if (host is not null) await host.DisposeAsync();
            shutdownComplete = true;
            Close();
        };
        FormClosed += (_, _) => { refresh.Dispose(); previewRefresh.Dispose(); };
    }

    private CaptureOptions RequestedCapture() => new((int)monitorFps.Value, (int)monitorWidth.Value, (int)monitorHeight.Value,
        Enum.Parse<CaptureMode>((string)captureMode.SelectedItem!));
    private void SetSelectedMonitoring(bool enabled)
    {
        if (host is null || SelectedId is not { } id) return;
        try { host.SetSessionMonitoring(id, enabled); RefreshMonitor(); }
        catch (Exception error) { monitorStatus.Text = "Monitor: " + error.Message; }
    }
    private void SetSelectedTaskbar(bool show)
    {
        if (host is null || SelectedId is not { } id) return;
        try
        {
            host.SetShowInTaskbar(id, show);
            if (show) taskbarHidden.Remove(id); else taskbarHidden.Add(id);
            RefreshSelection();
        }
        catch (Exception error) { status.Text = "Status: " + error.Message; }
    }

    private void RefreshSessions()
    {
        if (host is null) return;
        var current = host.GetSessions();
        for (var i = 0; i < current.Length; i++)
        {
            if (sessions.Rows.Count <= i) sessions.Rows.Add();
            var session = current[i];
            var geometry = host.GetWindow(session.AppSessionId);
            var placement = session.State == SessionState.Bound ? $"Bound / {geometry?.State.ToString() ?? "Mapping"}" : session.State.ToString();
            sessions.Rows[i].Tag = session.AppSessionId;
            sessions.Rows[i].SetValues(session.AppSessionId, session.WindowId?.ToString() ?? "—", placement, session.LaunchUrl);
            sessions.Rows[i].Cells[2].ToolTipText = geometry?.Error ?? session.Detail;
        }
        while (sessions.Rows.Count > current.Length) sessions.Rows.RemoveAt(sessions.Rows.Count - 1);
        if (pendingLaunchStatus is { } pending && host.GetSession(pending) is { } launched && launched.State != SessionState.Launching)
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
        var session = id is null ? null : host?.GetSession(id.Value);
        var geometry = id is null ? null : host?.GetWindow(id.Value);
        selected.Text = session is null ? "Select a session." : $"Window {session.WindowId} · {session.AppSessionId.ToString()[..8]}";
        var active = !closing && !operating && session?.State == SessionState.Bound && geometry is not null && geometry.State != PlacementState.Closed;
        parkButton.Enabled = active && geometry?.State == PlacementState.Visible;
        restoreButton.Enabled = active && geometry?.State != PlacementState.Visible;
        hideTaskbar.Enabled = active && id is { } sessionId && !taskbarHidden.Contains(sessionId);
        showTaskbar.Enabled = active && id is { } selectedId && taskbarHidden.Contains(selectedId);
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
        var snapshot = host.GetMonitorState();
        foreach (var session in snapshot.Sessions)
        {
            if (!tiles.TryGetValue(session.AppSessionId, out var tile))
            {
                tiles.Add(session.AppSessionId, tile = new MonitorTile());
                thumbnails.Controls.Add(tile); SizeTiles();
            }
            tile.Update(session, host.GetLatestFrame(session.AppSessionId), host.GetWindow(session.AppSessionId)?.State);
        }
        foreach (var id in tiles.Keys.Where(id => !snapshot.Sessions.Any(s => s.AppSessionId == id)).ToArray())
        {
            var tile = tiles[id]; thumbnails.Controls.Remove(tile); tile.Dispose(); tiles.Remove(id); SizeTiles();
        }
        monitorStatus.Text = $"Monitoring {(snapshot.Enabled ? "enabled" : "stopped")} · {snapshot.CapturingConnections} JPEG captures · {RequestedCapture().Mode} · Visible + Parked · human view only";
        monitorStart.Enabled = true; monitorStop.Enabled = monitorApply.Enabled = monitoring;
        var selectedMonitor = snapshot.Sessions.FirstOrDefault(s => s.AppSessionId == SelectedId);
        var canControl = monitoring && selectedMonitor is not null && selectedMonitor.State != "Unavailable";
        monitorPause.Enabled = canControl && selectedMonitor!.State != "Paused";
        monitorResume.Enabled = canControl && selectedMonitor!.State is "Paused" or "Error";
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
            var result = await Task.Run(() => park ? host.Park(id) : host.Restore(id));
            if (!closing) status.Text = $"Status: Window {result.WindowId} / {result.State} — {result.AppSessionId}";
        }
        catch (Exception error) { if (!closing) status.Text = "Status: " + error.Message; }
        finally { operating = false; if (!closing) { RefreshSessions(); refresh.Start(); } }
    }
}
