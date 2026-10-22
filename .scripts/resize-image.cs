#!/usr/bin/dotnet run

#:package SkiaSharp@4.*
#:package SkiaSharp.NativeAssets.Linux@4.*
#:package Svg.Skia@5.*
using SkiaSharp;
using Svg.Skia;

if (args.Length < 4)
{
  WriteHelp();
  return 1;
}

var sourcePath = args[0];
var outputPath = args[1];
if (!int.TryParse(args[2], out var width) || !int.TryParse(args[3], out var height) || width < 1 || height < 1)
{
  Console.WriteLine("Width and height must be positive integers.");
  WriteHelp();
  return 1;
}

if (!File.Exists(sourcePath))
{
  Console.WriteLine($"Source file does not exist: {sourcePath}");
  return 1;
}

var mode = args.Length > 4 ? args[4].ToLowerInvariant() : "exact";
if (mode is not ("exact" or "fit"))
{
  Console.WriteLine($"Unknown mode: {mode}");
  WriteHelp();
  return 1;
}

var fit = mode == "fit";
var outputDirectory = Path.GetDirectoryName(outputPath);

if (!string.IsNullOrEmpty(outputDirectory) && !Directory.Exists(outputDirectory))
{
  Directory.CreateDirectory(outputDirectory);
}

if (IsSvg(outputPath))
{
  return ResizeToSvg(sourcePath, outputPath, width, height, fit);
}

return ResizeToRaster(sourcePath, outputPath, width, height, fit);

// SVG output. SVG has no SKEncodedImageFormat, so the source is drawn onto
// an SKSvgCanvas instead of encoding a bitmap. An SVG source stays vector.
static int ResizeToSvg(string sourcePath, string outputPath, int width, int height, bool fit)
{
  using var outputStream = File.Create(outputPath);
  SKRect target;

  if (IsSvg(sourcePath))
  {
    using var svg = new SKSvg();
    var picture = svg.Load(sourcePath);
    if (picture == null)
    {
      return Fail($"Failed to decode SVG: {sourcePath}");
    }

    var (svgWidth, svgHeight) = FittedSize(picture.CullRect.Width, picture.CullRect.Height, width, height, fit);
    target = new SKRect(0, 0, svgWidth, svgHeight);
    using var canvas = SKSvgCanvas.Create(target, outputStream);
    DrawPictureScaled(canvas, picture, target);
  }
  else
  {
    using var original = LoadRasterBitmap(sourcePath);
    if (original == null)
    {
      return Fail($"Failed to decode image: {sourcePath}");
    }

    var (rasterWidth, rasterHeight) = FittedSize(original.Width, original.Height, width, height, fit);
    target = new SKRect(0, 0, rasterWidth, rasterHeight);
    using var canvas = SKSvgCanvas.Create(target, outputStream);
    canvas.DrawBitmap(original, target, LinearSampling());
  }

  Console.WriteLine($"Resized -> {outputPath} ({(int)target.Width}x{(int)target.Height})");
  return 0;
}

// Raster output. The format comes from the output extension. An SVG source
// is rendered straight at the target size, so it scales without resampling.
static int ResizeToRaster(string sourcePath, string outputPath, int width, int height, bool fit)
{
  var outputExt = Path.GetExtension(outputPath).TrimStart('.').ToLowerInvariant();
  var format = GetEncodedFormatFromExtension(outputExt);
  if (format == null)
  {
    Console.WriteLine($"Unsupported output format: {outputExt}");
    Console.WriteLine("Supported output formats: svg, jpg, jpeg, png, webp, bmp, ico, gif");
    return 1;
  }

  using var resized = IsSvg(sourcePath)
    ? RasterizeSvg(sourcePath, width, height, fit)
    : ResizeRaster(sourcePath, width, height, fit);
  if (resized == null)
  {
    return Fail($"Failed to resize image: {sourcePath}");
  }

  using var outputStream = File.Create(outputPath);
  if (IsAlphaCompatible(format.Value))
  {
    if (!resized.Encode(outputStream, format.Value, 100))
    {
      return Fail($"Failed to encode image: {outputPath}");
    }
  }
  else
  {
    // Formats without alpha get the image composited onto white first.
    using var flattened = FlattenOnWhite(resized);
    if (!flattened.Encode(outputStream, format.Value, 100))
    {
      return Fail($"Failed to encode image: {outputPath}");
    }
  }

  Console.WriteLine($"Resized -> {outputPath} ({resized.Width}x{resized.Height})");
  return 0;
}

static SKBitmap? ResizeRaster(string sourcePath, int width, int height, bool fit)
{
  using var original = LoadRasterBitmap(sourcePath);
  if (original == null)
  {
    return null;
  }

  var (targetWidth, targetHeight) = FittedSize(original.Width, original.Height, width, height, fit);
  var imageInfo = new SKImageInfo(targetWidth, targetHeight);
  return original.Resize(imageInfo, LinearSampling());
}

static SKBitmap? RasterizeSvg(string path, int width, int height, bool fit)
{
  using var svg = new SKSvg();
  var picture = svg.Load(path);
  if (picture == null)
  {
    return null;
  }

  var (targetWidth, targetHeight) = FittedSize(picture.CullRect.Width, picture.CullRect.Height, width, height, fit);
  var bitmap = new SKBitmap(targetWidth, targetHeight, SKColorType.Rgba8888, SKAlphaType.Premul);
  using var canvas = new SKCanvas(bitmap);
  canvas.Clear(SKColors.Transparent);
  DrawPictureScaled(canvas, picture, new SKRect(0, 0, targetWidth, targetHeight));
  canvas.Flush();
  return bitmap;
}

// Exact stretches the source into the box, which is what a square icon slot wants. Fit keeps the
// source aspect ratio and caps both sides at the box, so a wide logo stays wide.
static (int Width, int Height) FittedSize(double sourceWidth, double sourceHeight, int boxWidth, int boxHeight, bool fit)
{
  if (!fit || sourceWidth < 1 || sourceHeight < 1)
  {
    return (boxWidth, boxHeight);
  }

  var scale = Math.Min(boxWidth / sourceWidth, boxHeight / sourceHeight);
  return (Math.Max(1, (int)Math.Round(sourceWidth * scale)), Math.Max(1, (int)Math.Round(sourceHeight * scale)));
}

static void DrawPictureScaled(SKCanvas canvas, SKPicture picture, SKRect target)
{
  var bounds = picture.CullRect;
  canvas.Save();
  canvas.Translate(target.Left, target.Top);
  if (bounds.Width > 0 && bounds.Height > 0)
  {
    canvas.Scale(target.Width / bounds.Width, target.Height / bounds.Height);
    canvas.Translate(-bounds.Left, -bounds.Top);
  }
  canvas.DrawPicture(picture);
  canvas.Restore();
}

static bool IsSvg(string path)
{
  return Path.GetExtension(path).Equals(".svg", StringComparison.OrdinalIgnoreCase);
}

static SKBitmap? LoadRasterBitmap(string path)
{
  using var inputData = SKData.Create(path);
  using var codec = SKCodec.Create(inputData);
  if (codec == null)
  {
    return null;
  }

  return SKBitmap.Decode(codec);
}

static SKBitmap FlattenOnWhite(SKBitmap source)
{
  var flattened = new SKBitmap(source.Width, source.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
  using var canvas = new SKCanvas(flattened);
  canvas.DrawColor(SKColors.White);
  canvas.DrawBitmap(source, 0, 0, LinearSampling());
  return flattened;
}

static SKSamplingOptions LinearSampling()
{
  return new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear);
}

static bool IsAlphaCompatible(SKEncodedImageFormat format)
{
  return format is SKEncodedImageFormat.Png or SKEncodedImageFormat.Bmp or SKEncodedImageFormat.Ico;
}

static SKEncodedImageFormat? GetEncodedFormatFromExtension(string ext)
{
  return ext switch
  {
    "jpg" or "jpeg" => SKEncodedImageFormat.Jpeg,
    "png" => SKEncodedImageFormat.Png,
    "webp" => SKEncodedImageFormat.Webp,
    "bmp" => SKEncodedImageFormat.Bmp,
    "ico" => SKEncodedImageFormat.Ico,
    "gif" => SKEncodedImageFormat.Gif,
    _ => null,
  };
}

static int Fail(string message)
{
  Console.WriteLine(message);
  return 1;
}

static void WriteHelp()
{
  Console.WriteLine("Usage: resize-image <sourcePath> <outputPath> <width> <height> [exact|fit]");
  Console.WriteLine("Supported input formats include SVG and Skia-supported raster images.");
  Console.WriteLine("Supported output formats: svg, jpg, jpeg, png, webp, bmp, ico, gif");
  Console.WriteLine("Mode defaults to exact, which stretches to the given size. fit preserves the aspect ratio.");
  Console.WriteLine("Example: dotnet run \"{script_path}\" /path/to/input.svg /path/to/output.png 800 600");
}
