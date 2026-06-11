#!/usr/bin/dotnet run

#:package SkiaSharp
#:package SkiaSharp.NativeAssets.Linux
using SkiaSharp;

if (args.Length < 4)
{
  WriteHelp();
  return 1;
}

var sourcePath = args[0];
var outputPath = args[1];
if (!int.TryParse(args[2], out var width) || !int.TryParse(args[3], out var height))
{
  Console.WriteLine("Width and height must be valid integers.");
  WriteHelp();
  return 1;
}

if (!File.Exists(sourcePath))
{
  Console.WriteLine($"Source file does not exist: {sourcePath}");
  return 1;
}

using var inputData = SKData.Create(sourcePath);
using var codec = SKCodec.Create(inputData);
if (codec == null)
{
  Console.WriteLine($"Failed to decode image: {sourcePath}");
  return 1;
}

var format = codec.EncodedFormat;
using var original = SKBitmap.Decode(codec);
if (original == null)
{
  Console.WriteLine($"Failed to decode image: {sourcePath}");
  return 1;
}

var imageInfo = new SKImageInfo(width, height);
var samplingOptions = new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear);
using var resized = original.Resize(imageInfo, samplingOptions);
if (resized == null)
{
  Console.WriteLine("Failed to resize image.");
  return 1;
}

var outputDirectory = Path.GetDirectoryName(outputPath);
ArgumentNullException.ThrowIfNull(outputDirectory);

if (!Directory.Exists(outputDirectory))
{
  Directory.CreateDirectory(outputDirectory);
}

using var outputStream = File.Create(outputPath);
if (!resized.Encode(outputStream, format, 100))
{
  Console.WriteLine($"Failed to encode image: {outputPath}");
  return 1;
}

Console.WriteLine($"Resized -> {outputPath} ({width}x{height})");
return 0;

static void WriteHelp() {
  Console.WriteLine("Usage: resize-image <sourcePath> <outputPath> <width> <height>");
  Console.WriteLine("Example: dotnet run \"{script_path}\" /path/to/input.jpg /path/to/output.jpg 800 600");
}