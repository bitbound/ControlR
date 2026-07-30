using ControlR.DesktopClient.Common.Extensions;
using ControlR.DesktopClient.Common.Services;
using ControlR.Libraries.Api.Contracts.Dtos.RemoteControlDtos;
using ControlR.Libraries.Api.Contracts.Enums;
using MessagePack;
using SkiaSharp;

[assembly: CLSCompliant(false)]

namespace ControlR.Benchmarks.RemoteControl;

public class BandwidthComparison
{
  private const int FpsEstimate = 30;
  private const int HighQuality = 80;
  private const int LowQuality = 20;
  private const int ManualQuality = 75;

  public static void Run()
  {
    var imageUtility = new ImageUtility();
    var frames = LoadFrames(GetCapturesPath());
    var rng = new Random(42);

    var jpegFullFrameSizes = new List<long>();
    var webpFullFrameSizes = new List<long>();
    var jpegDirtyRegionSizes = new List<long>();
    var webpDirtyRegionSizes = new List<long>();
    var jpegMixedQualitySizes = new List<long>();
    var webpMixedQualitySizes = new List<long>();

    const int iterations = 50;

    for (var i = 0; i < iterations; i++)
    {
      var idx = rng.Next(0, frames.Length - 1);
      var current = frames[idx];
      var previous = frames[idx + 1];

      jpegFullFrameSizes.Add(MeasureFullFrame(current, ManualQuality, ImageFormat.Jpeg));
      webpFullFrameSizes.Add(MeasureFullFrame(current, ManualQuality, ImageFormat.WebP));

      var (jpegDirty, webpDirty) = MeasureDirtyRegions(current, previous, imageUtility, ManualQuality);
      jpegDirtyRegionSizes.Add(jpegDirty);
      webpDirtyRegionSizes.Add(webpDirty);

      var (jpegMixed, webpMixed) = MeasureMixedQuality(current, previous, imageUtility);
      jpegMixedQualitySizes.Add(jpegMixed);
      webpMixedQualitySizes.Add(webpMixed);
    }

    PrintResults("FULL FRAME ENCODING (single region covering entire display)",
      jpegFullFrameSizes, webpFullFrameSizes);

    PrintResults("DIRTY REGIONS (grid-diff, quality=75)",
      jpegDirtyRegionSizes, webpDirtyRegionSizes);

    PrintResults("MIXED QUALITY PATCHWORK (auto-quality simulation)",
      jpegMixedQualitySizes, webpMixedQualitySizes);

    PrintFpsBreakdown(jpegFullFrameSizes, webpFullFrameSizes, "FULL FRAME");
    PrintFpsBreakdown(jpegDirtyRegionSizes, webpDirtyRegionSizes, "DIRTY REGIONS");
    PrintFpsBreakdown(jpegMixedQualitySizes, webpMixedQualitySizes, "MIXED QUALITY");
  }

  private static byte[] EncodeRegion(
    SKBitmap bitmap, SKRect region, int quality, ImageFormat imageFormat)
  {
    var cropWidth = Math.Max(1, (int)region.Width);
    var cropHeight = Math.Max(1, (int)region.Height);
    var left = Math.Max(0, (int)region.Left);
    var top = Math.Max(0, (int)region.Top);
    left = Math.Min(left, bitmap.Width - cropWidth);
    top = Math.Min(top, bitmap.Height - cropHeight);

    using var cropped = new SKBitmap(cropWidth, cropHeight);
    using var canvas = new SKCanvas(cropped);
    canvas.DrawBitmap(
      bitmap,
      new SKRect(left, top, left + cropWidth, top + cropHeight),
      new SKRect(0, 0, cropWidth, cropHeight),
      SKSamplingOptions.Default);

    using var ms = new MemoryStream();
    var skFormat = imageFormat switch
    {
      ImageFormat.Jpeg => SKEncodedImageFormat.Jpeg,
      ImageFormat.WebP => SKEncodedImageFormat.Webp,
      ImageFormat.Png => SKEncodedImageFormat.Png,
      _ => throw new ArgumentOutOfRangeException(nameof(imageFormat), imageFormat, null)
    };
    cropped.Encode(ms, skFormat, quality);
    return ms.ToArray();
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

  private static SKBitmap[] LoadFrames(string directory)
  {
    var pngFiles = Directory.GetFiles(directory, "frame-*.png")
      .OrderBy(f => f, StringComparer.Ordinal)
      .ToArray();

    var bitmaps = new SKBitmap[pngFiles.Length];
    for (var i = 0; i < pngFiles.Length; i++)
    {
      using var stream = File.OpenRead(pngFiles[i]);
      using var data = SKBitmap.Decode(stream);
      bitmaps[i] = data.CopyEx();
    }

    return bitmaps;
  }

  private static (long jpeg, long webp) MeasureDirtyRegions(
    SKBitmap current, SKBitmap previous, IImageUtility imageUtility, int quality)
  {
    var diffResult = imageUtility.GetChangedAreas(current, previous);
    var dirtyRegions = diffResult.IsSuccess
      ? Array.FindAll(diffResult.Value, area => !area.IsEmpty)
      : [new SKRect(0, 0, current.Width, current.Height)];

    if (dirtyRegions.Length == 0)
    {
      dirtyRegions = [new SKRect(0, 0, current.Width, current.Height)];
    }

    return (MeasureRegions(current, dirtyRegions, quality, ImageFormat.Jpeg),
            MeasureRegions(current, dirtyRegions, quality, ImageFormat.WebP));
  }

  private static long MeasureFullFrame(SKBitmap bitmap, int quality, ImageFormat format)
  {
    var region = new SKRect(0, 0, bitmap.Width, bitmap.Height);
    var encoded = EncodeRegion(bitmap, region, quality, format);
    var dto = new ScreenRegionDto(
      0, 0,
      bitmap.Width, bitmap.Height,
      encoded,
      format);
    var wrapper = new ScreenRegionsDto([dto]);
    return MessagePackSerializer.Serialize(wrapper).Length;
  }

  private static (long jpeg, long webp) MeasureMixedQuality(
    SKBitmap current, SKBitmap previous, IImageUtility imageUtility)
  {
    var diffResult = imageUtility.GetChangedAreas(current, previous);
    var dirtyRegions = diffResult.IsSuccess
      ? Array.FindAll(diffResult.Value, area => !area.IsEmpty)
      : [new SKRect(0, 0, current.Width, current.Height)];

    if (dirtyRegions.Length == 0)
    {
      dirtyRegions = [new SKRect(0, 0, current.Width, current.Height)];
    }

    var half = dirtyRegions.Length / 2;
    var jpegDtos = new ScreenRegionDto[dirtyRegions.Length];
    var webpDtos = new ScreenRegionDto[dirtyRegions.Length];

    for (var i = 0; i < dirtyRegions.Length; i++)
    {
      var region = dirtyRegions[i];
      var q = i < half ? HighQuality : LowQuality;

      var jpegEncoded = EncodeRegion(current, region, q, ImageFormat.Jpeg);
      jpegDtos[i] = new ScreenRegionDto(
        (float)region.Left, (float)region.Top,
        (float)region.Width, (float)region.Height,
        jpegEncoded, ImageFormat.Jpeg);

      var webpEncoded = EncodeRegion(current, region, q, ImageFormat.WebP);
      webpDtos[i] = new ScreenRegionDto(
        (float)region.Left, (float)region.Top,
        (float)region.Width, (float)region.Height,
        webpEncoded, ImageFormat.WebP);
    }

    var jpegBytes = MessagePackSerializer.Serialize(new ScreenRegionsDto(jpegDtos)).Length;
    var webpBytes = MessagePackSerializer.Serialize(new ScreenRegionsDto(webpDtos)).Length;

    return (jpegBytes, webpBytes);
  }

  private static long MeasureRegions(SKBitmap bitmap, SKRect[] regions, int quality, ImageFormat format)
  {
    var dtos = new ScreenRegionDto[regions.Length];
    for (var i = 0; i < regions.Length; i++)
    {
      var region = regions[i];
      var encoded = EncodeRegion(bitmap, region, quality, format);
      dtos[i] = new ScreenRegionDto(
        (float)region.Left, (float)region.Top,
        (float)region.Width, (float)region.Height,
        encoded, format);
    }

    return MessagePackSerializer.Serialize(new ScreenRegionsDto(dtos)).Length;
  }

  private static void PrintFpsBreakdown(List<long> jpegSizes, List<long> webpSizes, string scenario)
  {
    var jpegAvg = jpegSizes.Average();
    var webpAvg = webpSizes.Average();
    var jpegMbps = jpegAvg * FpsEstimate * 8 / 1_000_000.0;
    var webpMbps = webpAvg * FpsEstimate * 8 / 1_000_000.0;

    Console.WriteLine();
    Console.WriteLine("{0} @ {1} fps estimate:", scenario, FpsEstimate);
    Console.WriteLine("  JPEG: {0:F2} Mbps", jpegMbps);
    Console.WriteLine("  WebP: {0:F2} Mbps", webpMbps);
    Console.WriteLine("  Savings: {0:F2} Mbps ({1:F1}%)", jpegMbps - webpMbps, (1 - webpMbps / jpegMbps) * 100);
  }

  private static void PrintResults(string scenario, List<long> jpegSizes, List<long> webpSizes)
  {
    var jpegAvg = jpegSizes.Average();
    var webpAvg = webpSizes.Average();
    var jpegMin = jpegSizes.Min();
    var webpMin = webpSizes.Min();
    var jpegMax = jpegSizes.Max();
    var webpMax = webpSizes.Max();
    var reduction = jpegAvg > 0 ? (1 - webpAvg / jpegAvg) * 100 : 0;

    Console.WriteLine();
    Console.WriteLine(scenario);
    Console.WriteLine("  JPEG: avg={0:N0}  min={1:N0}  max={2:N0} bytes/frame", jpegAvg, jpegMin, jpegMax);
    Console.WriteLine("  WebP: avg={0:N0}  min={1:N0}  max={2:N0} bytes/frame", webpAvg, webpMin, webpMax);
    Console.WriteLine("  Reduction: {0:F1}%  ({1:N0} bytes saved per frame)", reduction, jpegAvg - webpAvg);
  }
}

public class Program
{
  public static void Main()
  {
    BandwidthComparison.Run();

    Console.WriteLine();

    ChangedAreasBenchmark.Run();

    Console.WriteLine();

    EncodeSpeedBenchmark.Run();
  }
}
