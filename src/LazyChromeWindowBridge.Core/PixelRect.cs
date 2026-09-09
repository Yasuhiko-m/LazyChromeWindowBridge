namespace LazyChromeWindowBridge.Core;

public sealed record PixelRect(int Left, int Top, int Width, int Height)
{
    public long Right => (long)Left + Width;
    public long Bottom => (long)Top + Height;
    public bool Valid => Width >= 100 && Height >= 80 && Width <= 100000 && Height <= 100000 && Math.Abs((long)Left) <= 1000000 && Math.Abs((long)Top) <= 1000000;
    public bool Intersects(PixelRect other) => Left < other.Right && Right > other.Left && Top < other.Bottom && Bottom > other.Top;
    public bool Near(PixelRect other, int tolerance = 2) => Math.Abs((long)Left - other.Left) <= tolerance && Math.Abs((long)Top - other.Top) <= tolerance && Math.Abs((long)Width - other.Width) <= tolerance && Math.Abs((long)Height - other.Height) <= tolerance;
}
