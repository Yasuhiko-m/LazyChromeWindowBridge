using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
#if WINDOWS10_0_18362_0_OR_GREATER
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using WinRT;
#endif

namespace LazyChromeWindowBridge.Core;

internal sealed record NativeCaptureFrame(byte[] Jpeg, int Width, int Height, double CaptureMilliseconds);
internal interface INativeCaptureSession : IAsyncDisposable
{
    Task<NativeCaptureFrame> CaptureAsync(CaptureOptions options, CancellationToken token);
}
internal interface INativeCaptureFactory
{
    INativeCaptureSession Open(NativeIdentity identity);
}

#if WINDOWS10_0_18362_0_OR_GREATER
internal sealed class NativeWindowCaptureFactory : INativeCaptureFactory
{
    public INativeCaptureSession Open(NativeIdentity identity) => new NativeWindowCaptureSession(identity);
}

/// <summary>
/// Exact-HWND Windows Graphics Capture. The D3D11 video processor crops ContentSize and
/// downsizes into a bounded GPU texture; only that texture is mapped to CPU memory.
/// </summary>
internal sealed partial class NativeWindowCaptureSession : INativeCaptureSession
{
    private readonly GraphicsCaptureItem item;
    private readonly Direct3D11CaptureFramePool pool;
    private readonly GraphicsCaptureSession session;
    private readonly D3D11Scaler scaler;
    private readonly SemaphoreSlim arrived = new(0, 1);
    private readonly CancellationTokenSource closed = new();
    private readonly SemaphoreSlim captureGate = new(1, 1);
    private int disposed;

    public NativeWindowCaptureSession(NativeIdentity identity)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 18362) || !GraphicsCaptureSession.IsSupported())
            throw new PlatformNotSupportedException("NativeWindow requires Windows Graphics Capture on Windows 10 version 1903 or later.");
        try
        {
            scaler = new D3D11Scaler();
            item = CreateItemForWindow((nint)identity.Hwnd);
            if (item.Size.Width <= 0 || item.Size.Height <= 0) throw new InvalidOperationException("Owned native window has no capturable extent.");
            pool = Direct3D11CaptureFramePool.CreateFreeThreaded(scaler.WinRtDevice, DirectXPixelFormat.B8G8R8A8UIntNormalized, 2, item.Size);
            session = pool.CreateCaptureSession(item);
            pool.FrameArrived += FrameArrived;
            item.Closed += ItemClosed;
            session.StartCapture();
        }
        catch
        {
            scaler?.Dispose();
            throw;
        }
    }

    private void FrameArrived(Direct3D11CaptureFramePool sender, object args)
    {
        try { if (arrived.CurrentCount == 0) arrived.Release(); }
        catch (ObjectDisposedException) { }
    }
    private void ItemClosed(GraphicsCaptureItem sender, object args) => closed.Cancel();

    public async Task<NativeCaptureFrame> CaptureAsync(CaptureOptions options, CancellationToken token)
    {
        await captureGate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var watch = Stopwatch.StartNew();
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, closed.Token);
            await arrived.WaitAsync(linked.Token).ConfigureAwait(false);
            Direct3D11CaptureFrame? newest = null;
            try
            {
                while (pool.TryGetNextFrame() is { } next)
                {
                    newest?.Dispose();
                    newest = next;
                }
                if (newest is null) throw new InvalidOperationException("Windows Graphics Capture did not provide a frame.");
                var size = newest.ContentSize;
                if (size.Width <= 0 || size.Height <= 0) throw new InvalidOperationException("Windows Graphics Capture returned an empty frame.");
                var (width, height) = OutputSize(size.Width, size.Height, options.MaxWidth, options.MaxHeight);
                var jpeg = scaler.ResizeReadbackAndEncode(newest.Surface, size.Width, size.Height, width, height);
                watch.Stop();
                return new(jpeg, width, height, watch.Elapsed.TotalMilliseconds);
            }
            finally { newest?.Dispose(); }
        }
        catch (OperationCanceledException) when (closed.IsCancellationRequested && !token.IsCancellationRequested)
        {
            throw new InvalidOperationException("The exact native capture item closed.");
        }
        finally { captureGate.Release(); }
    }

    internal static (int Width, int Height) OutputSize(int width, int height, int maxWidth, int maxHeight)
    {
        var ratio = Math.Min(1d, Math.Min((double)maxWidth / width, (double)maxHeight / height));
        return (Math.Max(1, (int)(width * ratio)), Math.Max(1, (int)(height * ratio)));
    }

    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return ValueTask.CompletedTask;
        closed.Cancel();
        item.Closed -= ItemClosed;
        pool.FrameArrived -= FrameArrived;
        session.Dispose();
        pool.Dispose();
        scaler.Dispose();
        captureGate.Dispose();
        arrived.Dispose();
        closed.Dispose();
        return ValueTask.CompletedTask;
    }

    private static readonly Guid GraphicsCaptureItemId = new("79C3F95B-31F7-4EC2-A464-632EF5D30760");

    [GeneratedComInterface, Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal partial interface IGraphicsCaptureItemInterop
    {
        nint CreateForWindow(nint window, in Guid iid);
        nint CreateForMonitor(nint monitor, in Guid iid);
    }

    private static GraphicsCaptureItem CreateItemForWindow(nint hwnd)
    {
        var interop = GraphicsCaptureItem.As<IGraphicsCaptureItemInterop>();
        var pointer = interop.CreateForWindow(hwnd, GraphicsCaptureItemId);
        try { return MarshalInterface<GraphicsCaptureItem>.FromAbi(pointer); }
        finally { Marshal.Release(pointer); }
    }
}

internal sealed unsafe partial class D3D11Scaler : IDisposable
{
    private const uint Bgra8 = 87;
    private nint device, context, videoDevice, videoContext;
    internal IDirect3DDevice WinRtDevice { get; }

    public D3D11Scaler()
    {
        var result = D3D11CreateDevice(0, 1, 0, 0x20 | 0x800, 0, 0, 7, out device, out _, out context);
        Throw(result, "Cannot create the Direct3D 11 NativeWindow device.");
        try
        {
            videoDevice = Query(device, new("10EC4D5B-975A-4689-B9E4-D0AAC30FE333"));
            videoContext = Query(context, new("61F21C45-3C0E-4A74-9CEA-67100D9AD5E4"));
            var dxgi = Query(device, new("54EC77FA-1377-44E6-8C32-88FD5F44C84C"));
            try
            {
                Throw(CreateDirect3D11DeviceFromDXGIDevice(dxgi, out var inspectable), "Cannot bridge the D3D11 device to Windows Graphics Capture.");
                try { WinRtDevice = MarshalInterface<IDirect3DDevice>.FromAbi(inspectable); }
                finally { Marshal.Release(inspectable); }
            }
            finally { Marshal.Release(dxgi); }
        }
        catch { Dispose(); throw; }
    }

    [GeneratedComInterface, Guid("A9B3D012-3DF2-4EE3-B8D1-8695F457D3C1"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal partial interface IDirect3DDxgiInterfaceAccess
    {
        nint GetInterface(in Guid iid);
    }

    [StructLayout(LayoutKind.Sequential)] private struct Rational { public uint Numerator, Denominator; }
    [StructLayout(LayoutKind.Sequential)] private struct ContentDescription
    {
        public uint InputFrameFormat;
        public Rational InputFrameRate;
        public uint InputWidth, InputHeight;
        public Rational OutputFrameRate;
        public uint OutputWidth, OutputHeight, Usage;
    }
    [StructLayout(LayoutKind.Sequential)] private struct TextureDescription
    {
        public uint Width, Height, MipLevels, ArraySize, Format;
        public uint SampleCount, SampleQuality, Usage, BindFlags, CpuAccessFlags, MiscFlags;
    }
    [StructLayout(LayoutKind.Sequential)] private struct InputViewDescription
    { public uint FourCC, ViewDimension, MipSlice, ArraySlice; }
    [StructLayout(LayoutKind.Sequential)] private struct OutputViewDescription
    { public uint ViewDimension, MipSlice, FirstArraySlice, ArraySize; }
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct ProcessorStream
    {
        public int Enable;
        public uint OutputIndex, InputFrameOrField, PastFrames, FutureFrames;
        public nint PastSurfaces, InputSurface, FutureSurfaces, PastSurfacesRight, InputSurfaceRight, FutureSurfacesRight;
    }
    [StructLayout(LayoutKind.Sequential)] private struct MappedResource { public nint Data; public uint RowPitch, DepthPitch; }

    public byte[] ResizeReadbackAndEncode(IDirect3DSurface surface, int inputWidth, int inputHeight, int outputWidth, int outputHeight)
    {
        var access = surface.As<IDirect3DDxgiInterfaceAccess>();
        var textureId = new Guid("6F15AAF2-D208-4E89-9AB4-489535D34F9C");
        var source = access.GetInterface(textureId);
        nint enumerator = 0, processor = 0, inputView = 0, outputTexture = 0, outputView = 0, staging = 0;
        try
        {
            var content = new ContentDescription
            {
                InputFrameFormat = 0,
                InputFrameRate = new() { Numerator = 60, Denominator = 1 },
                InputWidth = (uint)inputWidth, InputHeight = (uint)inputHeight,
                OutputFrameRate = new() { Numerator = 60, Denominator = 1 },
                OutputWidth = (uint)outputWidth, OutputHeight = (uint)outputHeight,
                Usage = 2
            };
            Throw(CallOut(videoDevice, 10, &content, &enumerator), "D3D11 video scaling is unavailable for NativeWindow capture.");
            uint formatFlags = 0;
            Throw(Call(enumerator, 8, Bgra8, &formatFlags), "Cannot query the NativeWindow video format.");
            if ((formatFlags & 3) != 3) throw new PlatformNotSupportedException("The D3D11 device cannot scale BGRA NativeWindow frames.");
            Throw(CallOut(videoDevice, 4, enumerator, 0, &processor), "Cannot create the NativeWindow video processor.");

            var gpu = new TextureDescription { Width = (uint)outputWidth, Height = (uint)outputHeight, MipLevels = 1, ArraySize = 1,
                Format = Bgra8, SampleCount = 1, Usage = 0, BindFlags = 0x20 };
            Throw(CallOut(device, 5, &gpu, 0, &outputTexture), "Cannot allocate the bounded NativeWindow GPU surface.");
            var input = new InputViewDescription { ViewDimension = 1 };
            Throw(CallOut(videoDevice, 8, source, enumerator, &input, &inputView), "Cannot bind the NativeWindow capture surface.");
            var output = new OutputViewDescription { ViewDimension = 1 };
            Throw(CallOut(videoDevice, 9, outputTexture, enumerator, &output, &outputView), "Cannot bind the bounded NativeWindow output surface.");

            var sourceRect = new NativeRect { Right = inputWidth, Bottom = inputHeight };
            var destinationRect = new NativeRect { Right = outputWidth, Bottom = outputHeight };
            CallVoid(videoContext, 13, processor, 1, &destinationRect);
            CallVoid(videoContext, 30, processor, 0, 1, &sourceRect);
            CallVoid(videoContext, 31, processor, 0, 1, &destinationRect);
            var stream = new ProcessorStream { Enable = 1, InputSurface = inputView };
            Throw(CallBlt(videoContext, 53, processor, outputView, 0, 1, &stream), "NativeWindow GPU resize failed.");

            var cpu = gpu; cpu.Usage = 3; cpu.BindFlags = 0; cpu.CpuAccessFlags = 0x20000;
            Throw(CallOut(device, 5, &cpu, 0, &staging), "Cannot allocate the bounded NativeWindow readback surface.");
            CallVoid(context, 47, staging, outputTexture);
            var mapped = new MappedResource();
            Throw(Call(context, 14, staging, 0, 1, 0, &mapped), "Cannot map the bounded NativeWindow frame.");
            try { return EncodeJpeg(mapped, outputWidth, outputHeight); }
            finally { CallVoid(context, 15, staging, 0); }
        }
        finally
        {
            Release(staging); Release(outputView); Release(outputTexture); Release(inputView); Release(processor); Release(enumerator); Release(source);
        }
    }

    private static byte[] EncodeJpeg(MappedResource mapped, int width, int height)
    {
        using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        var bits = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try
        {
            for (var row = 0; row < height; row++)
                Buffer.MemoryCopy((byte*)mapped.Data + row * mapped.RowPitch, (byte*)bits.Scan0 + row * bits.Stride, bits.Stride, width * 4L);
        }
        finally { bitmap.UnlockBits(bits); }
        using var output = new MemoryStream();
        using var encoding = new EncoderParameters(1);
        encoding.Param[0] = new EncoderParameter(Encoder.Quality, 70L);
        bitmap.Save(output, ImageCodecInfo.GetImageEncoders().Single(codec => codec.FormatID == ImageFormat.Jpeg.Guid), encoding);
        return output.ToArray();
    }

    public void Dispose()
    {
        if (WinRtDevice is IDisposable disposable) disposable.Dispose();
        Release(videoContext); videoContext = 0;
        Release(videoDevice); videoDevice = 0;
        Release(context); context = 0;
        Release(device); device = 0;
    }

    private static nint Query(nint value, Guid iid)
    {
        nint result = 0;
        Throw(Call(value, 0, &iid, &result), "Required Direct3D 11 interface is unavailable.");
        return result;
    }
    private static nint Slot(nint value, int index) => ((nint*)(*(nint*)value))[index];
    private static int Call(nint value, int slot, Guid* iid, nint* result)
        => ((delegate* unmanaged[Stdcall]<nint, Guid*, nint*, int>)Slot(value, slot))(value, iid, result);
    private static int Call(nint value, int slot, uint argument, uint* result)
        => ((delegate* unmanaged[Stdcall]<nint, uint, uint*, int>)Slot(value, slot))(value, argument, result);
    private static int CallBlt(nint value, int slot, nint processor, nint view, uint outputFrame, uint streamCount, ProcessorStream* streams)
        => ((delegate* unmanaged[Stdcall]<nint, nint, nint, uint, uint, ProcessorStream*, int>)Slot(value, slot))(value, processor, view, outputFrame, streamCount, streams);
    private static int Call(nint value, int slot, nint resource, uint subresource, uint mapType, uint flags, MappedResource* mapped)
        => ((delegate* unmanaged[Stdcall]<nint, nint, uint, uint, uint, MappedResource*, int>)Slot(value, slot))(value, resource, subresource, mapType, flags, mapped);
    private static int CallOut(nint value, int slot, ContentDescription* description, nint* result)
        => ((delegate* unmanaged[Stdcall]<nint, ContentDescription*, nint*, int>)Slot(value, slot))(value, description, result);
    private static int CallOut(nint value, int slot, nint a, uint b, nint* result)
        => ((delegate* unmanaged[Stdcall]<nint, nint, uint, nint*, int>)Slot(value, slot))(value, a, b, result);
    private static int CallOut(nint value, int slot, TextureDescription* description, nint initial, nint* result)
        => ((delegate* unmanaged[Stdcall]<nint, TextureDescription*, nint, nint*, int>)Slot(value, slot))(value, description, initial, result);
    private static int CallOut(nint value, int slot, nint resource, nint enumerator, InputViewDescription* description, nint* result)
        => ((delegate* unmanaged[Stdcall]<nint, nint, nint, InputViewDescription*, nint*, int>)Slot(value, slot))(value, resource, enumerator, description, result);
    private static int CallOut(nint value, int slot, nint resource, nint enumerator, OutputViewDescription* description, nint* result)
        => ((delegate* unmanaged[Stdcall]<nint, nint, nint, OutputViewDescription*, nint*, int>)Slot(value, slot))(value, resource, enumerator, description, result);
    private static void CallVoid(nint value, int slot, nint a, int b, NativeRect* c)
        => ((delegate* unmanaged[Stdcall]<nint, nint, int, NativeRect*, void>)Slot(value, slot))(value, a, b, c);
    private static void CallVoid(nint value, int slot, nint a, uint b, int c, NativeRect* d)
        => ((delegate* unmanaged[Stdcall]<nint, nint, uint, int, NativeRect*, void>)Slot(value, slot))(value, a, b, c, d);
    private static void CallVoid(nint value, int slot, nint a, nint b)
        => ((delegate* unmanaged[Stdcall]<nint, nint, nint, void>)Slot(value, slot))(value, a, b);
    private static void CallVoid(nint value, int slot, nint a, uint b)
        => ((delegate* unmanaged[Stdcall]<nint, nint, uint, void>)Slot(value, slot))(value, a, b);
    private static void Throw(int result, string message)
    {
        if (result < 0) throw new InvalidOperationException(message, Marshal.GetExceptionForHR(result));
    }
    private static void Release(nint value) { if (value != 0) Marshal.Release(value); }

    [DllImport("d3d11.dll", CallingConvention = CallingConvention.StdCall)]
    private static extern int D3D11CreateDevice(nint adapter, uint driverType, nint software, uint flags, nint featureLevels,
        uint featureLevelCount, uint sdkVersion, out nint device, out uint featureLevel, out nint immediateContext);
    [DllImport("d3d11.dll", CallingConvention = CallingConvention.StdCall)]
    private static extern int CreateDirect3D11DeviceFromDXGIDevice(nint dxgiDevice, out nint graphicsDevice);
}
#else
internal sealed class NativeWindowCaptureFactory : INativeCaptureFactory
{
    public INativeCaptureSession Open(NativeIdentity identity) =>
        throw new PlatformNotSupportedException("NativeWindow requires Windows Graphics Capture on Windows 10 version 1903 or later; BrowserViewport remains available.");
}
#endif
