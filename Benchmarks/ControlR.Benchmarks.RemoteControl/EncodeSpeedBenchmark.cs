using System.Diagnostics;
using ControlR.DesktopClient.Common.Extensions;
using SkiaSharp;

namespace ControlR.Benchmarks.RemoteControl;

public class EncodeSpeedBenchmark
{
  private const int Iterations = 100;

  private static readonly int[] _qualityLevels = [20, 50, 75, 90];

  public static void Run()
  {
    var capturesPath = GetCapturesPath();
    var frames = LoadFrames(capturesPath, 4);
    if (frames.Length == 0)
    {
      Console.WriteLine("No capture frames for encode speed benchmarking.");
      return;
    }

    var bitmap = frames[0];
    Console.WriteLine();
    Console.WriteLine("=== Encode Speed: JPEG vs WebP ===");
    Console.WriteLine("  Resolution: {0}x{1}, {2} iterations per measurement", bitmap.Width, bitmap.Height, Iterations);

    var fullRect = new SKRect(0, 0, bitmap.Width, bitmap.Height);
    var quarterWidth = bitmap.Width / 2;
    var quarterHeight = bitmap.Height / 2;

    Console.WriteLine();
    Console.WriteLine("  Full-frame encode (single huge rect):");
    MeasureFormats(bitmap, fullRect, "    ");

    Console.WriteLine();
    Console.WriteLine("  Quarter-frame encode (typical dirty rect):");
    var quarterRect = new SKRect(0, 0, quarterWidth, quarterHeight);
    MeasureFormats(bitmap, quarterRect, "    ");
  }

  private static string GetCapturesPath()
  {
    var current = AppContext.BaseDirectory;
    while (!string.IsNullOrEmpty(current))
    {
      var candidate = Path.Combine(current, ".plans", "captures");
      if (Directory.Exists(candidate))
      {
        return candidate;
      }
      var parent = Directory.GetParent(current);
      current = parent?.FullName;
    }
    throw new DirectoryNotFoundException(".plans/captures directory not found");
  }

  private static SKBitmap[] LoadFrames(string directory, int count)
  {
    var pngFiles = Directory.GetFiles(directory, "frame-*.png")
      .OrderBy(f => f, StringComparer.Ordinal)
      .ToArray();

    var bitmaps = new SKBitmap[Math.Min(count, pngFiles.Length)];
    for (var i = 0; i < bitmaps.Length; i++)
    {
      using var stream = File.OpenRead(pngFiles[i]);
      using var data = SKBitmap.Decode(stream);
      bitmaps[i] = data.CopyEx();
    }

    return bitmaps;
  }

  private static double MeasureEncodeTime(
    SKBitmap bitmap, SKRect region, int quality, SKEncodedImageFormat format)
  {
    var cropWidth = Math.Max(1, (int)region.Width);
    var cropHeight = Math.Max(1, (int)region.Height);
    var left = Math.Max(0, (int)region.Left);
    var top = Math.Max(0, (int)region.Top);
    left = Math.Min(left, bitmap.Width - cropWidth);
    top = Math.Min(top, bitmap.Height - cropHeight);

    var times = new double[Iterations];

    for (var i = 0; i < Iterations; i++)
    {
      using var cropped = new SKBitmap(cropWidth, cropHeight);
      using var canvas = new SKCanvas(cropped);
      canvas.DrawBitmap(
        bitmap,
        new SKRect(left, top, left + cropWidth, top + cropHeight),
        new SKRect(0, 0, cropWidth, cropHeight),
        SKSamplingOptions.Default);

      using var ms = new MemoryStream();
      var sw = Stopwatch.StartNew();
      cropped.Encode(ms, format, quality);
      sw.Stop();
      times[i] = sw.Elapsed.TotalMicroseconds;
    }

    Array.Sort(times);
    var trimStart = Iterations / 10;
    var trimEnd = Iterations - trimStart;
    return times[trimStart..trimEnd].Average();
  }

  private static void MeasureFormats(SKBitmap bitmap, SKRect region, string indent)
  {
    Console.WriteLine("{0}{1,-10} {2,10} {3,10} {4,10} {5,10}", indent, "Quality", "JPEG (us)", "WebP (us)", "Ratio", "JPEG faster");

    foreach (var quality in _qualityLevels)
    {
      var jpegTime = MeasureEncodeTime(bitmap, region, quality, SKEncodedImageFormat.Jpeg);
      var webpTime = MeasureEncodeTime(bitmap, region, quality, SKEncodedImageFormat.Webp);
      var ratio = webpTime / jpegTime;

      Console.WriteLine("{0}q={1,-8} {2,10:F1} {3,10:F1} {4,10:F2}x {5,10:F0}%",
        indent, quality, jpegTime, webpTime, ratio, (ratio - 1) * 100);
    }
  }
}
