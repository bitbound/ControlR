
using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.DesktopClient.Linux;
using ControlR.DesktopClient.Linux.Services;
using ControlR.Libraries.DevicesCommon.Services;
using ControlR.Libraries.NativeInterop.Unix;
using ControlR.Libraries.Shared.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SkiaSharp;

var builder = Host.CreateApplicationBuilder(args);
var desktopEnvironemnt = DesktopEnvironmentDetector.Instance.GetDesktopEnvironment();

builder.Services.AddSingleton<IFileSystem, FileSystem>();

// Register services based on detected desktop environment
switch (desktopEnvironemnt)
{
  case DesktopEnvironmentType.Wayland:
    builder.Services
      .AddSingleton<IDisplayManager, DisplayManagerWayland>()
      .AddSingleton<IScreenGrabber, ScreenGrabberWayland>()
      .AddSingleton<IInputSimulator, InputSimulatorWayland>()
      .AddSingleton<IWaylandPermissionProvider, WaylandPermissionProvider>();
    break;
  case DesktopEnvironmentType.X11:
    builder.Services
      .AddSingleton<IDisplayManager, DisplayManagerX11>()
      .AddSingleton<IScreenGrabber, ScreenGrabberX11>()
      .AddSingleton<IInputSimulator, InputSimulatorX11>()
      .AddHostedService<CursorWatcherX11>();
    break;
  default:
    throw new NotSupportedException("Unsupported desktop environment detected.");
}

// Common services
builder.Services.AddSingleton<IDesktopEnvironmentDetector, DesktopEnvironmentDetector>();
builder.Services.AddSingleton<IFileSystemUnix, FileSystemUnix>();

var app = builder.Build();

var logger = app.Services.GetRequiredService<ILogger<Program>>();

var screenGrabber = app.Services.GetRequiredService<IScreenGrabber>();
var captureResult = await screenGrabber.CaptureAllDisplays();
if (!captureResult.IsSuccess)
{
  logger.LogError("Screen capture failed: {ErrorMessage}", captureResult.FailureReason);
  return;
}

var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
logger.LogInformation("Saving screenshot to {DesktopPath}/screenshot.jpg", desktopPath);
await using var fs = File.OpenWrite(Path.Combine(desktopPath, "screenshot.jpg"));

var encodeResult = captureResult.Bitmap.Encode(fs, SKEncodedImageFormat.Jpeg, 90);
if (encodeResult)
{
  logger.LogInformation("Screenshot dimensions: {Width}x{Height}", captureResult.Bitmap.Width, captureResult.Bitmap.Height);
}
else
{
  logger.LogError("Failed to encode screenshot.");
}