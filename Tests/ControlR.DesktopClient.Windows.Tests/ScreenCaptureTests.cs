using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.DesktopClient.Windows.Services;
using ControlR.Libraries.NativeInterop.Windows;
using ControlR.Tests.TestingUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Xunit.Abstractions;
#if IsWindows
using Devolutions.Cadeau;
#endif

namespace ControlR.DesktopClient.Windows.Tests;

[SupportedOSPlatform("windows8.0")]
public class ScreenCaptureTests
{
  private readonly IHost _host;
  private readonly IScreenGrabber _screenGrabber;

  public ScreenCaptureTests(ITestOutputHelper testOutput)
  {
    var builder = Host.CreateApplicationBuilder();
    builder.Services
      .AddSingleton<IWin32Interop, Win32Interop>()
      .AddSingleton<InputSimulatorWindows>()
      .AddSingleton<IInputSimulator>(services => services.GetRequiredService<InputSimulatorWindows>())
      .AddSingleton<ICaptureMetrics, CaptureMetricsWindows>()
      .AddSingleton<IClipboardManager, ClipboardManagerWindows>()
      .AddSingleton<IDisplayManager, DisplayManagerWindows>()
      .AddSingleton<IScreenGrabber, ScreenGrabberWindows>()
      .AddSingleton<IDxOutputDuplicator, DxOutputDuplicator>();

    builder.Logging.AddProvider(new XunitLoggerProvider(testOutput));

    _host = builder.Build();
    _screenGrabber = _host.Services.GetRequiredService<IScreenGrabber>();
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

#if IsWindows
  [InteractiveWindowsFact]
  public async Task ScreenGrabber_EncodeViaCadeau()
  {
    // Get initial capture to determine frame dimensions
    using var initialCapture = await _screenGrabber.CaptureAllDisplays();
    Assert.True(initialCapture.IsSuccess, "Initial capture failed.");
    Assert.NotNull(initialCapture.Bitmap);

    var frameWidth = (uint)initialCapture.Bitmap.Width;
    var frameHeight = (uint)initialCapture.Bitmap.Height;
    uint frameRate = 60;
    var captureDuration = TimeSpan.FromSeconds(1);
    
    // Setup output file on desktop
    string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
    string outputFile = Path.Combine(desktopPath, "CadeauTest.webm");

    // Initialize XmfRecorder
    var recorder = new XmfRecorder();
    recorder.SetFileName(outputFile);
    recorder.SetVideoQuality(10);
    recorder.SetFrameSize(frameWidth, frameHeight);
    recorder.SetFrameRate(frameRate);
    recorder.Init();

    try
    {
      var startTime = DateTimeOffset.UtcNow;
      var endTime = startTime.Add(captureDuration);

      while (DateTimeOffset.UtcNow < endTime)
      {
        // Capture the current frame
        using var captureResult = await _screenGrabber.CaptureAllDisplays();
        
        if (!captureResult.IsSuccess || captureResult.Bitmap is null)
        {
          continue;
        }

        unsafe
        {
          // Get the pixel data from SKBitmap
          IntPtr pixelPtr = captureResult.Bitmap.GetPixels();
          
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
      initialCapture.Dispose();
    }
  }
  #endif

  [InteractiveWindowsFact]
  public async Task ScreenGrabber_EncodeViaFfmpeg()
  {
    // Get initial capture to determine frame dimensions
    using var initialCapture = await _screenGrabber.CaptureAllDisplays();
    Assert.True(initialCapture.IsSuccess, "Initial capture failed.");
    Assert.NotNull(initialCapture.Bitmap);

    var frameWidth = initialCapture.Bitmap.Width;
    var frameHeight = initialCapture.Bitmap.Height;
    var frameRate = 20;
    var captureDuration = TimeSpan.FromSeconds(1);

    // Calculate frame interval
    var frameIntervalMs = 1000 / frameRate;

    // Setup output file on desktop
    var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
    var outputFile = Path.Combine(desktopPath, "FfmpegTest.webm");

    // Delete existing output file if it exists
    if (File.Exists(outputFile))
    {
      File.Delete(outputFile);
    }

    // Configure ffmpeg process
    // Input: raw BGRA video from stdin
    // Output: WebM VP9 encoded video to stdout (pipe:1), which this test will capture and write to a file
    // -y flag: overwrite output file without asking
    var ffmpegArgs = $"-y -f rawvideo -pixel_format bgra -video_size {frameWidth}x{frameHeight} " +
             $"-framerate {frameRate} -i pipe:0 -g {frameRate * 2} " +
             $"-vf \"format=yuv420p\" -c:v libvpx-vp9 -deadline realtime -row-mt 1 " +
             $"-f webm pipe:1";

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
    await using var outputFs = new FileStream(outputFile, FileMode.Create, FileAccess.Write, FileShare.None);

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
      var nextFrame = DateTimeOffset.UtcNow;
      var startTime = DateTimeOffset.UtcNow;
      var endTime = startTime.Add(captureDuration);

      var frameSize = frameWidth * frameHeight * 4; // BGRA = 4 bytes per pixel
      var frameBuffer = new byte[frameSize];
      var framesWritten = 0;

      while (DateTimeOffset.UtcNow < endTime)
      {
        while (DateTimeOffset.UtcNow < nextFrame)
        {
          // Wait until it's time for the next frame
          await Task.Delay(1);
        }
        nextFrame = nextFrame.AddMilliseconds(frameIntervalMs);

        // Check if ffmpeg process has exited
        if (ffmpegProcess.HasExited)
        {
          var stderr = await stderrTask;
          Assert.Fail($"FFmpeg process exited prematurely after {framesWritten} frames. Exit code: {ffmpegProcess.ExitCode}\n\nStderr output:\n{stderr}");
        }

        // Capture the current frame
        using var captureResult = await _screenGrabber.CaptureAllDisplays();

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
      }

      // Close stdin to signal end of input
      ffmpegProcess.StandardInput.Close();

      // Wait for stderr reading to complete and ffmpeg to finish encoding (with timeout)
      var finishTimeout = TimeSpan.FromSeconds(10);
      var finished = await Task.Run(() => ffmpegProcess.WaitForExit((int)finishTimeout.TotalMilliseconds));
      
      if (!finished)
      {
        var stderr = stderrTask.IsCompleted ? await stderrTask : "stderr not available";
        Assert.Fail($"FFmpeg did not finish within the timeout period.\n\nStderr output:\n{stderr}");
      }

      var stderrOutput = await stderrTask;

      // Check exit code
      if (ffmpegProcess.ExitCode != 0)
      {
        Assert.Fail($"FFmpeg exited with error code {ffmpegProcess.ExitCode}.\n\nStderr output:\n{stderrOutput}");
      }

      // Wait for the stdout copy to complete to ensure file has all bytes
      await stdoutCopyTask;

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
