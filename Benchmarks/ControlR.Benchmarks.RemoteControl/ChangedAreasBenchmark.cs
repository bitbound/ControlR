using System.Diagnostics;
using ControlR.DesktopClient.Common.Extensions;
using ControlR.DesktopClient.Common.Services;
using SkiaSharp;

namespace ControlR.Benchmarks.RemoteControl;

public class ChangedAreasBenchmark
{
  private const int Iterations = 100;

  public static void Run()
  {
    var capturesPath = GetCapturesPath();
    var (frames, _) = LoadFramePairs(capturesPath, 6);
    if (frames.Length < 2)
    {
      Console.WriteLine("Not enough capture frames for benchmarking.");
      return;
    }

    var imageUtility = new ImageUtility();
    var current = frames[0];
    var previous = frames[1];

    WarmUp(imageUtility, current, previous);

    var simdTimes = new List<double>();
    var scalarTimes = new List<double>();

    for (var i = 0; i < Iterations; i++)
    {
      var sw = Stopwatch.StartNew();
      imageUtility.GetChangedAreas(current, previous);
      sw.Stop();
      simdTimes.Add(sw.Elapsed.TotalMicroseconds);
    }

    for (var i = 0; i < Iterations; i++)
    {
      var sw = Stopwatch.StartNew();
      imageUtility.GetChangedAreas(current, previous, useSimd: false);
      sw.Stop();
      scalarTimes.Add(sw.Elapsed.TotalMicroseconds);
    }

    PrintResults("SIMD (Vector<byte>)", simdTimes, scalarTimes);
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

  private static (SKBitmap[], string[]) LoadFramePairs(string directory, int count)
  {
    var pngFiles = Directory.GetFiles(directory, "frame-*.png")
      .OrderBy(f => f, StringComparer.Ordinal)
      .ToArray();

    if (pngFiles.Length == 0)
    {
      throw new DirectoryNotFoundException($"No PNG frames found in {directory}");
    }

    var bitmaps = new SKBitmap[Math.Min(count, pngFiles.Length)];
    for (var i = 0; i < bitmaps.Length; i++)
    {
      using var stream = File.OpenRead(pngFiles[i]);
      using var data = SKBitmap.Decode(stream);
      bitmaps[i] = data.CopyEx();
    }

    return (bitmaps, pngFiles.Take(bitmaps.Length).ToArray());
  }

  private static void PrintResults(string simdLabel, List<double> simdTimes, List<double> scalarTimes)
  {
    var simdAvg = simdTimes.Average();
    var scalarAvg = scalarTimes.Average();
    var speedup = scalarAvg / simdAvg;

    Console.WriteLine();
    Console.WriteLine("=== Change Detection: Scalar vs SIMD ===");
    Console.WriteLine();
    Console.WriteLine("Scenario: {0} grid (4 columns x 2 rows), {1} iterations", "4x2", Iterations);
    Console.WriteLine();
    Console.WriteLine("  Scalar:  avg={0:F1} us  min={1:F1} us  max={2:F1} us",
      scalarAvg, scalarTimes.Min(), scalarTimes.Max());
    Console.WriteLine("  SIMD:    avg={0:F1} us  min={1:F1} us  max={2:F1} us",
      simdAvg, simdTimes.Min(), simdTimes.Max());
    Console.WriteLine("  Speedup: {0:F2}x", speedup);
    Console.WriteLine("  Savings: {0:F1}%", (1 - simdAvg / scalarAvg) * 100);
  }

  private static void WarmUp(IImageUtility imageUtility, SKBitmap current, SKBitmap previous)
  {
    for (var i = 0; i < 5; i++)
    {
      imageUtility.GetChangedAreas(current, previous);
      imageUtility.GetChangedAreas(current, previous, useSimd: false);
    }
  }
}
