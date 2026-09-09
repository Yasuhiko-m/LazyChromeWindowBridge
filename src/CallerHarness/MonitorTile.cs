namespace CallerHarness;

internal sealed class MonitorTile : Panel
{
    private readonly Label title = new() { Dock = DockStyle.Top, Height = 22, AutoEllipsis = true };
    private readonly Label status = new() { Dock = DockStyle.Bottom, Height = 36, AutoEllipsis = true };
    private readonly PictureBox image = new() { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.FromArgb(25, 25, 25) };
    private (long Generation, long Sequence) displayed;
    public MonitorTile()
    {
        Margin = new Padding(4); Padding = new Padding(4); BorderStyle = BorderStyle.FixedSingle;
        Controls.Add(image); Controls.Add(title); Controls.Add(status);
    }
    public void Update(SessionMonitorSnapshot state, MonitorFrame? frame)
    {
        title.Text = $"{state.AppSessionId.ToString()[..8]} · Window {state.WindowId}";
        AccessibleName = title.Text;
        image.AccessibleName = "Human thumbnail " + state.AppSessionId.ToString("D");
        if (frame is null) ClearImage();
        else if (displayed != (frame.Generation, frame.Sequence))
        {
            using var stream = new MemoryStream(frame.Jpeg);
            using var decoded = Image.FromStream(stream);
            var old = image.Image; image.Image = new Bitmap(decoded); old?.Dispose();
            displayed = (frame.Generation, frame.Sequence);
        }
        var age = state.LastFrameAt is { } timestamp ? $" · {(DateTimeOffset.UtcNow - timestamp).TotalSeconds:F1}s" : "";
        status.Text = state.State == "ACTIVE" ? "ACTIVE · no capture" :
            $"{(state.State == "Live" ? "PARKED / LIVE" : state.State)} · {state.Frames} frames{age}" +
            (state.Error is null ? "" : "\n" + state.Error);
    }
    private void ClearImage() { var old = image.Image; image.Image = null; old?.Dispose(); displayed = default; }
    protected override void Dispose(bool disposing) { if (disposing) ClearImage(); base.Dispose(disposing); }
}
