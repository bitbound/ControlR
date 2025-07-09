using System.Diagnostics;
using System.Runtime.Versioning;
using ControlR.Agent.Common.Interfaces;
using ControlR.Devices.Native.Services;
using ControlR.Agent.Common.Models;
using ControlR.Libraries.Shared.Constants;
using Result = ControlR.Libraries.Shared.Primitives.Result;
using Microsoft.Extensions.Options;

namespace ControlR.Agent.Common.Services.Windows;

[SupportedOSPlatform("windows6.0.6000")]
internal class StreamerLauncherWindows(
  IWin32Interop win32Interop,
  IProcessManager processes,
  ISystemEnvironment environment,
  IStreamingSessionCache streamingSessionCache,
  IFileSystem fileSystem,
  ISettingsProvider settings,
  ILogger<StreamerLauncherWindows> logger) : IStreamerLauncher
{
  private readonly SemaphoreSlim _createSessionLock = new(1, 1);
  private readonly IWin32Interop _win32Interop = win32Interop;
  private readonly IProcessManager _processManager = processes;
  private readonly ISystemEnvironment _environment = environment;
  private readonly IStreamingSessionCache _streamingSessionCache = streamingSessionCache;
  private readonly IFileSystem _fileSystem = fileSystem;
  private readonly ISettingsProvider _settings = settings;
  private readonly ILogger<StreamerLauncherWindows> _logger = logger;

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
      var session = new StreamingSession(viewerConnectionId);

      var dataFolder = _settings.InstanceId is { Length: > 0 } instanceId
        ? instanceId
        : "Default";

      var args =
        $"--session-id {sessionId} --data-folder \"{dataFolder}\" --websocket-uri \"{websocketUri}\" --notify-user {notifyViewerOnSessionStart}";
      if (!string.IsNullOrWhiteSpace(viewerName))
      {
        args += $" --viewer-name=\"{viewerName}\"";
      }

      _logger.LogInformation("Launching remote control with args: {StreamerArguments}", args);

      if (_environment.IsDebug)
      {
        session.StreamerProcess = StartDebugSession(args);
        if (session.StreamerProcess is null)
        {
          return Result.Fail("Failed to start remote control process.");
        }
      }
      else
      {
        var startupDir = _environment.StartupDirectory;
        var streamerDir = Path.Combine(startupDir, "Streamer");
        var binaryPath = Path.Combine(streamerDir, AppConstants.StreamerFileName);

        Process? process;
        bool result;

        if (targetWindowsSession == 0)
        {
          var shellPath = Path.Combine(
            _environment.SelfExtractDir,
            "Embedded",
            "BackgroundShell",
            "ControlR.BackgroundShell.exe");

          var backgroundShells = _processManager
            .GetProcessesByName("ControlR.BackgroundShell")
            .Where(x => x.SessionId == targetWindowsSession)
            .ToArray();

          foreach (var shell in backgroundShells)
          {
            shell.KillAndDispose();
          }

          _logger.LogInformation(
            "Starting background shell for session {SessionId} at {ShellPath}.",
            targetWindowsSession,
            shellPath);

          if (!_win32Interop.StartProcessInBackgroundSession(
                commandLine: shellPath,
                hiddenWindow: false,
                startedProcess: out _))
          {
            return Result.Fail("Failed to start background shell.");
          }

          result = _win32Interop.StartProcessInBackgroundSession(
            commandLine: $"\"{binaryPath}\" {args}",
            hiddenWindow: true,
            startedProcess: out process);
        }
        else
        {
          result = _win32Interop.CreateInteractiveSystemProcess(
            commandLine: $"\"{binaryPath}\" {args}",
            targetSessionId: targetWindowsSession,
            hiddenWindow: true,
            startedProcess: out process);
        }

        if (!result || process is null || process.Id == -1)
        {
          _logger.LogError("Failed to start remote control process.");
          return Result.Fail("Failed to start remote control process.");
        }

        session.StreamerProcess = process;
      }

      await _streamingSessionCache.AddOrUpdate(session);

      return Result.Ok();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while creating remote control session.");
      return Result.Fail("An error occurred while starting remote control.");
    }
    finally
    {
      _createSessionLock.Release();
    }
  }

  private Process? StartDebugSession(string args)
  {
    var solutionDirReult = GetSolutionDir(Environment.CurrentDirectory);

    if (!solutionDirReult.IsSuccess)
    {
      return null;
    }

    var streamerBin = Path.Combine(
          solutionDirReult.Value,
          "ControlR.Streamer",
          "bin",
          "Debug");

    var streamerPath = _fileSystem
      .GetFiles(streamerBin, AppConstants.StreamerFileName, SearchOption.AllDirectories)
      .OrderByDescending(x => new FileInfo(x).CreationTime)
      .FirstOrDefault();

    if (string.IsNullOrWhiteSpace(streamerPath))
    {
      throw new FileNotFoundException("Streamer binary not found.", streamerPath);
    }

    var psi = new ProcessStartInfo()
    {
      WorkingDirectory = Path.GetDirectoryName(streamerPath),
      UseShellExecute = true
    };

    if (_settings.CloseStreamerConsoleOnExit)
    {
      psi.FileName = streamerPath;
      psi.Arguments = args;
    }
    else
    {
      psi.FileName = "cmd.exe";
      psi.Arguments = $"/k {streamerPath} {args}";
    }

    return _processManager.Start(psi);
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
}