using ControlR.Libraries.ScreenCapture.Models;
using System.Diagnostics;
using System.Drawing.Imaging;
using ControlR.Libraries.ScreenCapture.Extensions;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Drawing;
using ControlR.Libraries.ScreenCapture.Helpers;
using Microsoft.Extensions.Logging;
using ControlR.Libraries.Shared.Services.Testable;

namespace ControlR.Libraries.ScreenCapture.Benchmarks;
public sealed class CaptureTests
{
    private readonly LoggerFactory _loggerFactory;
    private readonly BitmapUtility _bitmapUtility;
    private readonly TestableSystemTime _systemTime;
    private readonly DxOutputGenerator _dxGenerator;
    private readonly ScreenCapturer _capturer;
    private readonly IEnumerable<DisplayInfo> _displays;

    public CaptureTests()
    {
        _loggerFactory = new LoggerFactory();
        _bitmapUtility = new BitmapUtility();
        _systemTime = new TestableSystemTime();
        _dxGenerator = new DxOutputGenerator(_loggerFactory.CreateLogger<DxOutputGenerator>());
        _capturer = new ScreenCapturer(_bitmapUtility, _dxGenerator, _systemTime, _loggerFactory.CreateLogger<ScreenCapturer>());
        _displays = _capturer.GetDisplays();
    }


    public void DoCaptureEncodeAndDiff()
    {
        var count = 300;
        var sw = Stopwatch.StartNew();
        var display1 = _displays.First(x => x.DeviceName == "\\\\.\\DISPLAY1");
        CaptureResult? lastResult = null;
        byte[] bytes = [];

        for (var i = 0; i < count; i++)
        {
            var result = _capturer.Capture(display1, true, tryUseDirectX: true, allowFallbackToBitBlt: true);
            if (!result.IsSuccess)
            {
                continue;
            }

            try
            {
                if (result.IsUsingGpu)
                {
                    foreach (var dirtyRect in result.DirtyRects)
                    {
                        using var cropped = _bitmapUtility.CropBitmap(result.Bitmap, dirtyRect);
                        bytes = _bitmapUtility.Encode(cropped, ImageFormat.Jpeg);
                    }
                }
                else
                {
                    var diffArea = _bitmapUtility.GetChangedArea(result.Bitmap, lastResult?.Bitmap);
                    if (!diffArea.IsSuccess)
                    {
                        continue;
                    }

                    if (diffArea.Value.IsEmpty)
                    {
                        continue;
                    }

                    using var cropped = _bitmapUtility.CropBitmap(result.Bitmap, diffArea.Value);
                    bytes = _bitmapUtility.Encode(cropped, ImageFormat.Jpeg);
                }

            }
            finally
            {
                lastResult?.Dispose();
                lastResult = result;
            }
        }

        var fps = Math.Round(count / sw.Elapsed.TotalSeconds);
        Console.WriteLine($"Capture + Encode + Diff FPS: {fps}");
    }

    //[Benchmark(OperationsPerInvoke = 1)]
    public void DoCaptures()
    {
        var count = 300;
        var sw = Stopwatch.StartNew();
        var display1 = _displays.First(x => x.DeviceName == "\\\\.\\DISPLAY1");

        for (var i = 0; i < count; i++)
        {
            using var result = _capturer.Capture(display1, true, tryUseDirectX: true, allowFallbackToBitBlt: false);
        }

        var fps = Math.Round(count / sw.Elapsed.TotalSeconds);
        Console.WriteLine($"Capture FPS: {fps}");
    }

    public void DoDiffSizeComparison()
    {
        var count = 300;
        var sw = Stopwatch.StartNew();
        var display1 = _displays.First(x => x.DeviceName == "\\\\.\\DISPLAY1");
        CaptureResult? lastResult = null;
        byte[] bytes = [];
        double gpuDiffSize = 0;
        double gpuTotalFrames = 0;

        double cpuDiffSize = 0;
        double cpuTotalFrames = 0;

        for (var i = 0; i < count; i++)
        {
            var result = _capturer.Capture(display1, true, tryUseDirectX: true, allowFallbackToBitBlt: false);
            if (!result.IsSuccess)
            {
                continue;
            }

            try
            {
                foreach (var dirtyRect in result.DirtyRects)
                {
                    using var cropped = _bitmapUtility.CropBitmap(result.Bitmap, dirtyRect);
                    bytes = _bitmapUtility.Encode(cropped, ImageFormat.Jpeg);
                    gpuDiffSize += bytes.Length;
                    gpuTotalFrames++;
                }
            }
            finally
            {
                lastResult?.Dispose();
                lastResult = result;
            }
        }

        for (var i = 0; i < count; i++)
        {
            var result = _capturer.Capture(display1, true, tryUseDirectX: true, allowFallbackToBitBlt: false);
            if (!result.IsSuccess)
            {
                continue;
            }

            try
            {
                var diffArea = _bitmapUtility.GetChangedArea(result.Bitmap, lastResult?.Bitmap);
                if (!diffArea.IsSuccess)
                {
                    continue;
                }

                if (diffArea.Value.IsEmpty)
                {
                    continue;
                }

                using var cropped = _bitmapUtility.CropBitmap(result.Bitmap, diffArea.Value);
                bytes = _bitmapUtility.Encode(cropped, ImageFormat.Jpeg);
                cpuDiffSize += bytes.Length;
                cpuTotalFrames++;
            }
            finally
            {
                lastResult?.Dispose();
                lastResult = result;
            }
        }

        var gpuDiffPerFrame = Math.Round(gpuDiffSize / gpuTotalFrames);
        var cpuDiffPerFrame = Math.Round(cpuDiffSize / cpuTotalFrames);

        Console.WriteLine($"GPU Frames: {gpuTotalFrames} | GPU Size per Frame: {gpuDiffPerFrame}");
        Console.WriteLine($"CPU Frames: {cpuTotalFrames} | CPU Size per Frame: {cpuDiffPerFrame}");
    }

    public void DoEncoding()
    {
        var count = 300;
        var display1 = _displays.First(x => x.DeviceName == "\\\\.\\DISPLAY1");
        using var result = _capturer.Capture(display1, true, tryUseDirectX: true, allowFallbackToBitBlt: true);
        if (!result.IsSuccess)
        {
            throw new Exception("Capture failed.");
        }

        var sw = Stopwatch.StartNew();
        for (var i = 0; i < count; i++)
        {
            _ = _bitmapUtility.Encode(result.Bitmap, ImageFormat.Jpeg);
        }

        var encodeTime = Math.Round(sw.Elapsed.TotalMilliseconds / count, 2);
        Console.WriteLine($"Encode Time: {encodeTime}ms");
    }
    public void DoSaveToDesktop()
    {
        var display1 = _displays.First(x => x.DeviceName == "\\\\.\\DISPLAY1");
        using var result = _capturer.Capture(display1);
        if (!result.IsSuccess)
        {
            throw new Exception("Capture failed.");
        }

        var encoded = _bitmapUtility.EncodeJpeg(result.Bitmap, 25);
        var profilePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        File.WriteAllBytes($"{profilePath}\\Desktop\\Test.jpg", encoded);

        encoded = _bitmapUtility.EncodeJpeg(result.Bitmap, 75);
        File.WriteAllBytes($"{profilePath}\\Desktop\\Test2.jpg", encoded);
    }

    public async Task DoWinRtComparison()
    {
        var iterations = 300;
        var display1 = _displays.First(x => x.DeviceName == "\\\\.\\DISPLAY1");
        using var result = _capturer.Capture(display1);
        if (!result.IsSuccess)
        {
            throw new Exception("Capture failed.");
        }

        var sbBuffer = new byte[result.Bitmap.Width * result.Bitmap.Height * Image.GetPixelFormatSize(result.Bitmap.PixelFormat)];
        using var sb = result.Bitmap.ToSoftwareBitmap();
        sb.CopyFromBuffer(sbBuffer.AsBuffer());

        var tps = await GetTimesPerSecond(() =>
        {
            for (var i = 0; i < iterations; i++)
            {
                _ = _bitmapUtility.EncodeJpeg(result.Bitmap, 70);
            }
            return Task.FromResult(iterations);
        });

        var winRtTps = await GetTimesPerSecond(async () =>
        {
            for (var i = 0; i < iterations; i++)
            {
                _ = await _bitmapUtility.EncodeJpegWinRt(sb, .7);
            }
            return iterations;
        });

        Console.WriteLine($"Win32 TPS: {tps} | WinRt TPS: {winRtTps}");
    }

    private static async Task<double> GetTimesPerSecond(Func<Task<int>> func)
    {
        var sw = Stopwatch.StartNew();
        var count = await func();
        var timesPerSecond = Math.Round(count / sw.Elapsed.TotalSeconds, 2);
        return timesPerSecond;
    }
}
