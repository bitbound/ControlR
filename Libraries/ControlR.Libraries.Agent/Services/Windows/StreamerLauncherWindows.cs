using System.Diagnostics;
using System.Runtime.Versioning;
using ControlR.Devices.Native.Services;
using ControlR.Libraries.Agent.Interfaces;
using ControlR.Libraries.Agent.IpcDtos;
using ControlR.Libraries.Agent.Models;
using ControlR.Libraries.Shared.Constants;
using ControlR.Libraries.Shared.Extensions;
using ControlR.Libraries.Shared.Primitives;
using Microsoft.Extensions.Hosting;
using SimpleIpc;
using Result = ControlR.Libraries.Shared.Primitives.Result;

namespace ControlR.Libraries.Agent.Services.Windows;

[SupportedOSPlatform("windows6.0.6000")]
internal class StreamerLauncherWindows(
  IHostApplicationLifetime appLifetime,
  IWin32Interop win32Interop,
  IProcessManager processes,
  IIpcRouter ipcRouter,
  ISystemEnvironment environment,
  IStreamingSessionCache streamingSessionCache,
  ISettingsProvider settings,
  IFileSystem fileSystem,
  ILogger<StreamerLauncherWindows> logger) : IStreamerLauncher
{
  private readonly SemaphoreSlim _createSessionLock = new(1, 1);

  public async Task<Result> CreateSession(
    Guid sessionId,
    Uri websocketUri,
    string viewerConnectionId,
    int targetWindowsSession = -1,
    bool notifyViewerOnSessionStart = false,
    string? viewerName = null)
  {
    await _createSessionLock.WaitAsync();

    try
    {
      var echoResult = await GetCurrentInputDesktop(targetWindowsSession);

      if (!echoResult.IsSuccess)
      {
        logger.LogResult(echoResult);
        return Result.Fail("Failed to determine initial input desktop.");
      }

      var targetDesktop = echoResult.Value.Trim();
      logger.LogInformation("Starting streamer in desktop: {Desktop}", targetDesktop);

      var session = new StreamingSession(viewerConnectionId);

      var serverUri = settings.ServerUri.ToString().TrimEnd('/');
      var args =
        $"--session-id {sessionId} --viewer-id {viewerConnectionId} --origin {serverUri} --websocket-uri {websocketUri} --notify-user {notifyViewerOnSessionStart}";
      if (!string.IsNullOrWhiteSpace(viewerName))
      {
        args += $" --viewer-name=\"{viewerName}\"";
      }

      logger.LogInformation("Launching remote control with args: {StreamerArguments}", args);

      if (!environment.IsDebug)
      {
        var startupDir = environment.StartupDirectory;
        var streamerDir = Path.Combine(startupDir, "Streamer");
        var binaryPath = Path.Combine(streamerDir, AppConstants.StreamerFileName);

        win32Interop.CreateInteractiveSystemProcess(
          $"\"{binaryPath}\" {args}",
          targetWindowsSession,
          false,
          targetDesktop,
          true,
          out var process);

        if (process is null || process.Id == -1)
        {
          var streamerZipPath = Path.Combine(startupDir, AppConstants.StreamerZipFileName);
          // Delete streamer files so a clean install will be performed on the next attempt.
          fileSystem.DeleteDirectory(streamerDir, true);
          fileSystem.DeleteFile(streamerZipPath);
          return Result.Fail("Failed to start remote control process.");
        }

        session.StreamerProcess = process;
      }
      else
      {
        var solutionDirReult = GetSolutionDir(Environment.CurrentDirectory);

        if (solutionDirReult.IsSuccess)
        {
          var streamerBin = Path.Combine(
            solutionDirReult.Value,
            "ControlR.Streamer",
            "bin",
            "Debug");

          var streamerPath = fileSystem
            .GetFiles(streamerBin, AppConstants.StreamerFileName, SearchOption.AllDirectories)
            .FirstOrDefault();

          if (string.IsNullOrWhiteSpace(streamerPath))
          {
            throw new FileNotFoundException("Streamer binary not found.", streamerPath);
          }

          var psi = new ProcessStartInfo
          {
            FileName = streamerPath,
            Arguments = args,
            WorkingDirectory = Path.GetDirectoryName(streamerPath),
            UseShellExecute = true
          };
          //var psi = new ProcessStartInfo()
          //{
          //    FileName = "cmd.exe",
          //    Arguments = $"/k {streamerPath} {args}",
          //    WorkingDirectory = Path.GetDirectoryName(streamerPath),
          //    UseShellExecute = true
          //};
          session.StreamerProcess = processes.Start(psi);
        }

        if (session.StreamerProcess is null)
        {
          return Result.Fail("Failed to start remote control process.");
        }
      }

      await streamingSessionCache.AddOrUpdate(session);

      return Result.Ok();
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Error while creating remote control session.");
      return Result.Fail("An error occurred while starting remote control.");
    }
    finally
    {
      _createSessionLock.Release();
    }
  }

  // For debugging.
  private static Result<string> GetSolutionDir(string currentDir)
  {
    var dirInfo = new DirectoryInfo(currentDir);
    if (!dirInfo.Exists)
    {
      return Result.Fail<string>("Not found.");
    }

    if (dirInfo.GetFiles().Any(x => x.Name == "ControlR.sln"))
    {
      return Result.Ok(currentDir);
    }

    if (dirInfo.Parent is not null)
    {
      return GetSolutionDir(dirInfo.Parent.FullName);
    }

    return Result.Fail<string>("Not found.");
  }

  private async Task<Result<string>> GetCurrentInputDesktop(int targetWindowsSession)
  {
    var pipeName = Guid.NewGuid().ToString();
    using var ipcServer = await ipcRouter.CreateServer(pipeName);

    Process? process = null;

    if (Environment.UserInteractive)
    {
      process = processes.Start(
        environment.StartupExePath,
        $"echo-desktop --pipe-name {pipeName}");
    }
    else
    {
      win32Interop.CreateInteractiveSystemProcess(
        $"\"{environment.StartupExePath}\" echo-desktop --pipe-name {pipeName}",
        targetWindowsSession,
        false,
        "Default",
        true,
        out process);
    }

    if (process is null || process.HasExited)
    {
      return Result.Fail<string>("Failed to start echo-desktop process.");
    }

    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(appLifetime.ApplicationStopping, cts.Token);
    process.Exited += (_, _) => linkedCts.Cancel();
    if (!await ipcServer.WaitForConnection(linkedCts.Token))
    {
      return Result.Fail<string>("Failed to connect to echo-desktop process.");
    }

    ipcServer.BeginRead(appLifetime.ApplicationStopping);
    logger.LogInformation("Desktop echoer connected to pipe server.");
    var desktopResult = await ipcServer.Invoke<DesktopRequestDto, DesktopResponseDto>(new DesktopRequestDto());
    if (desktopResult.IsSuccess)
    {
      logger.LogInformation("Received current input desktop from echoer: {CurrentDesktop}",
        desktopResult.Value.DesktopName);
      await ipcServer.Send(new ShutdownRequestDto());
      return Result.Ok(desktopResult.Value.DesktopName);
    }

    logger.LogError("Failed to get initial desktop from desktop echo.");
    return Result.Fail<string>(desktopResult.Error);
  }
}