using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.DesktopClient.Windows.Services;
using ControlR.Libraries.NativeInterop.Windows;
using ControlR.Libraries.TestingUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Xunit;
using ControlR.Libraries.Shared.Extensions;
using ControlR.Libraries.Shared.Helpers;

#if IS_WINDOWS
using Devolutions.Cadeau;
#endif

namespace ControlR.DesktopClient.Windows.Tests;

[SupportedOSPlatform("windows8.0")]
public class ScreenCaptureTests
{
  private readonly TimeSpan _captureDuration = TimeSpan.FromSeconds(10);
  private readonly IDisplayManagerWindows _displayManager;
  private readonly IHost _host;
  private readonly IScreenGrabber _screenGrabber;

  public ScreenCaptureTests(ITestOutputHelper testOutput)
  {
    var builder = Host.CreateApplicationBuilder();
    builder.Services
      .AddSingleton<IWindowsMessagePump, WindowsMessagePump>()
      .AddSingleton<IWin32Interop, Win32Interop>()
      .AddSingleton<InputSimulatorWindows>()
      .AddSingleton<IInputSimulator>(services => services.GetRequiredService<InputSimulatorWindows>())
      .AddSingleton<ICaptureMetrics, CaptureMetricsWindows>()
      .AddSingleton<IClipboardManager, ClipboardManagerWindows>()
      .AddSingleton<IDisplayManager, DisplayManagerWindows>()
      .AddSingleton<IScreenGrabber, ScreenGrabberWindows>()
      .AddSingleton<IDisplayManagerWindows, DisplayManagerWindows>()
      .AddSingleton<IDxOutputDuplicator, DxOutputDuplicator>();

    builder.Logging.AddProvider(new XunitLoggerProvider(testOutput));

    _host = builder.Build();
    _screenGrabber = _host.Services.GetRequiredService<IScreenGrabber>();
    _displayManager = _host.Services.GetRequiredService<IDisplayManagerWindows>();
  }

  [InteractiveWindowsFact]
  public async Task ScreenGrabber_CaptureAllDisplays_Ok()
  {
    using var captureResult = await _screenGrabber.CaptureAllDisplays();
    Assert.True(captureResult.IsSuccess, "Capture failed.");
    Assert.NotNull(captureResult.Bitmap);
    Assert.True(captureResult.Bitmap.Width > 0, "Bitmap is width is 0.");
    Assert.True(captureResult.Bitmap.Height > 0, "Bitmap height is 0.");
  }

#if IS_WINDOWS
  [InteractiveWindowsFact]
  public async Task ScreenGrabber_EncodeViaCadeau()
  {
    // Get initial capture to determine frame dimensions
    var display = await _displayManager.GetPrimaryDisplay();
    Guard.IsNotNull(display, "No primary display found.");

    var frameWidth = (uint)display.CapturePixelSize.Width;
    var frameHeight = (uint)display.CapturePixelSize.Height;
    uint frameRate = 60;

    // Setup output file on desktop
    var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
    var outputFile = Path.Combine(desktopPath, "CadeauTest.webm");

    // Initialize XmfRecorder
    using var recorder = new XmfRecorder();
    using var mkvStream = new XmfMkvStream();
    recorder.SetBipBuffer(mkvStream.BipBuffer);
    //recorder.SetFileName(outputFile);
    recorder.SetVideoQuality(10);
    recorder.SetFrameSize(frameWidth, frameHeight);
    recorder.SetFrameRate(frameRate);
    recorder.Init();

    try
    {

      using var cts = new CancellationTokenSource(_captureDuration);
      while (!cts.IsCancellationRequested)
      {

        // Capture the current frame
        using var captureResult = await _screenGrabber.CaptureDisplay(display, forceKeyFrame: true);

        if (!captureResult.IsSuccess || captureResult.Bitmap is null)
        {
          continue;
        }

        unsafe
        {
          // Get the pixel data from SKBitmap
          var pixelPtr = captureResult.Bitmap.GetPixels();

          // Update the recorder with the frame
          recorder.UpdateFrame(
            pixelPtr,
            0,
            0,
            frameWidth,
            frameHeight,
            frameWidth * 4); // stride = width * 4 bytes per pixel (BGRA)

          // Signal timeout to encode the frame
          recorder.Timeout();
        }
      }

      recorder.Timeout();

      using var fs = File.Open(outputFile, FileMode.Create, FileAccess.ReadWrite);
      await mkvStream.CopyToAsync(fs);

      // Verify the output file was created
      Assert.True(File.Exists(outputFile), $"Output file was not created at {outputFile}");

      // Verify the file has content
      var fileInfo = new FileInfo(outputFile);
      Assert.True(fileInfo.Length > 0, "Output file is empty.");
    }
    finally
    {
      // Cleanup recorder
      recorder.Uninit();
    }
  }

#endif

  [InteractiveWindowsFact]
  public async Task ScreenGrabber_EncodeViaFfmpeg()
  {
    // Get initial capture to determine frame dimensions
    var display = await _displayManager.GetPrimaryDisplay();
    Guard.IsNotNull(display, "No primary display found.");
    using var initialCapture = await _screenGrabber.CaptureDisplay(display, forceKeyFrame: true);
    Assert.True(initialCapture.IsSuccess, "Initial capture failed.");
    Assert.NotNull(initialCapture.Bitmap);

    var frameWidth = initialCapture.Bitmap.Width;
    var frameHeight = initialCapture.Bitmap.Height;
    var frameRate = 20;

    // Calculate frame interval
    var frameInterval = TimeSpan.FromMilliseconds(1000.0 / frameRate);

    // Setup output file on desktop
    var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
    var outputFile = Path.Combine(desktopPath, "FfmpegTest.webm");

    // Delete existing output file if it exists
    if (File.Exists(outputFile))
    {
      File.Delete(outputFile);
    }

    // Configure ffmpeg process
    var ffmpegArgs = $"-y -use_wallclock_as_timestamps 1 -f rawvideo -pixel_format bgra " +
                    $"-video_size {frameWidth}x{frameHeight} -i pipe:0 " +
                    $"-c:v libvpx-vp9 -deadline realtime -cpu-used 8 -row-mt 1 " +
                    $"-vf \"format=yuv420p\" -g 60 -vsync vfr -f webm pipe:1";

    var processStartInfo = new ProcessStartInfo
    {
      FileName = "ffmpeg",
      Arguments = ffmpegArgs,
      UseShellExecute = false,
      RedirectStandardInput = true,
      RedirectStandardOutput = true,
      RedirectStandardError = true,
      CreateNoWindow = true
    };

    using var ffmpegProcess = Process.Start(processStartInfo);
    Assert.NotNull(ffmpegProcess);
    Assert.NotNull(ffmpegProcess.StandardInput);

    // Capture stderr output for diagnostics (read to end asynchronously)
    var stderrTask = ffmpegProcess.StandardError.ReadToEndAsync();

    // Open file for writing the streamed stdout from ffmpeg
    await using var outputFs = new FileStream(
      outputFile, 
      FileMode.Create, 
      FileAccess.Write, 
      FileShare.None);

    // Copy stdout bytes to file in the background while we feed FFmpeg with frames
    var stdoutCopyTask = Task.Run(async () =>
    {
      try
      {
        await ffmpegProcess.StandardOutput.BaseStream.CopyToAsync(outputFs);
      }
      catch (Exception ex)
      {
        // If copying fails, surface a helpful assertion-like exception later
        throw new IOException("Failed to copy ffmpeg stdout to file", ex);
      }
    });

    try
    {
      var frameSize = frameWidth * frameHeight * 4; // BGRA = 4 bytes per pixel
      var frameBuffer = new byte[frameSize];
      var framesWritten = 0;

      using var cts = new CancellationTokenSource(_captureDuration);
      
      while (!cts.IsCancellationRequested)
      {
        // Check if ffmpeg process has exited
        if (ffmpegProcess.HasExited)
        {
          var stderr = await stderrTask;
          Assert.Fail($"FFmpeg process exited prematurely after {framesWritten} frames. Exit code: {ffmpegProcess.ExitCode}\n\nStderr output:\n{stderr}");
        }

        // Capture the current frame
        using var captureResult = await _screenGrabber.CaptureDisplay(display, forceKeyFrame: true);

        if (!captureResult.IsSuccess || captureResult.Bitmap is null)
        {
          continue;
        }

        // Copy pixel data to buffer (outside unsafe context to allow async)
        unsafe
        {
          IntPtr pixelPtr = captureResult.Bitmap.GetPixels();
          Marshal.Copy(pixelPtr, frameBuffer, 0, frameSize);
        }

        try
        {
          // Write frame to ffmpeg stdin
          await ffmpegProcess.StandardInput.BaseStream.WriteAsync(frameBuffer.AsMemory(0, frameSize));
          await ffmpegProcess.StandardInput.BaseStream.FlushAsync();
          framesWritten++;
        }
        catch (IOException ex) when (ex.Message.Contains("pipe is being closed"))
        {
          // FFmpeg closed the pipe - check if it's an error or normal completion
          var stderr = await stderrTask;
          Assert.Fail($"FFmpeg closed pipe after {framesWritten} frames. This may indicate an error.\n\nStderr output:\n{stderr}\n\nException: {ex.Message}");
        }
        await Task.Delay(frameInterval);
      }

      // Close stdin to signal end of input
      await ffmpegProcess.StandardInput.BaseStream.FlushAsync();
      ffmpegProcess.StandardInput.Close();

      // Wait for stderr reading to complete and ffmpeg to finish encoding
      // The stdoutCopyTask completes when ffmpeg finishes writing to stdout (after encoding)
      var stderrOutput = await stderrTask;
      await stdoutCopyTask;

      // Check exit code
      if (ffmpegProcess.ExitCode != 0)
      {
        Assert.Fail($"FFmpeg exited with error code {ffmpegProcess.ExitCode}.\n\nStderr output:\n{stderrOutput}");
      }

      // Verify the output file was created
      Assert.True(File.Exists(outputFile), $"Output file was not created at {outputFile}");

      // Verify the file has content
      var fileInfo = new FileInfo(outputFile);
      Assert.True(fileInfo.Length > 0, $"Output file is empty. Frames written: {framesWritten}\n\nFFmpeg stderr:\n{stderrOutput}");
    }
    finally
    {
      initialCapture.Dispose();

      // Ensure ffmpeg process is terminated
      if (!ffmpegProcess.HasExited)
      {
        ffmpegProcess.Kill(entireProcessTree: true);
      }

      // Ensure stdout copy task is observed; if it failed it will propagate here
      try
      {
        if (!stdoutCopyTask.IsCompleted)
        {
          await stdoutCopyTask;
        }
      }
      catch
      {
        // Ignore here; assertions above will fail if copy failed
      }
    }
  }
}
