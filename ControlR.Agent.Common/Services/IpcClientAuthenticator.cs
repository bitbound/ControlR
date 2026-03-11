using System.Diagnostics;
using System.Collections.Concurrent;
using ControlR.Agent.Common.Interfaces;
using ControlR.Libraries.Ipc;
using ControlR.Libraries.Shared.Helpers;

namespace ControlR.Agent.Common.Services;

public interface IIpcClientAuthenticator
{
  Task<Result<IpcClientCredentials>> AuthenticateConnection(IIpcServer server);
  Task<Result> CheckRateLimit(string executablePath);
  Task RecordFailedAttempt(string executablePath);
}

public class IpcClientAuthenticator(
  TimeProvider timeProvider,
  IIpcClientCredentialsProvider credentialsProvider,
  ISystemEnvironment systemEnvironment,
  IDesktopClientFileVerifier fileVerifier,
  IFileSystemPathProvider pathProvider,
  ILogger<IpcClientAuthenticator> logger) : IIpcClientAuthenticator
{
  private const int MaxFailuresPerMinute = 5;

  private readonly IIpcClientCredentialsProvider _credentialsProvider = credentialsProvider;
  private readonly ConcurrentDictionary<string, List<DateTimeOffset>> _failedAttempts = new();
  private readonly IDesktopClientFileVerifier _fileVerifier = fileVerifier;
  private readonly ILogger<IpcClientAuthenticator> _logger = logger;
  private readonly IFileSystemPathProvider _pathProvider = pathProvider;
  private readonly SemaphoreSlim _rateLimitLock = new(1, 1);
  private readonly ISystemEnvironment _systemEnvironment = systemEnvironment;
  private readonly TimeProvider _timeProvider = timeProvider;

  public async Task<Result<IpcClientCredentials>> AuthenticateConnection(IIpcServer server)
  {
    var authenticationStopwatch = Stopwatch.StartNew();

    try
    {
      // 1. Get client credentials from pipe/socket handle
      var credentialsStopwatch = Stopwatch.StartNew();
      var credsResult = _credentialsProvider.GetClientCredentials(server);
      _logger.LogInformation(
        "IPC credential lookup completed in {ElapsedMs} ms.",
        credentialsStopwatch.ElapsedMilliseconds);

      if (!credsResult.IsSuccess)
      {
        _logger.LogCritical(
          "Failed to get IPC client credentials after {ElapsedMs} ms: {Reason}",
          authenticationStopwatch.ElapsedMilliseconds,
          credsResult.Reason);
        return Result.Fail<IpcClientCredentials>(credsResult.Reason);
      }

      var credentials = credsResult.Value;

      _logger.LogInformation(
        "IPC connection attempt from PID {ProcessId}, Path: {ExecutablePath}",
        credentials.ProcessId,
        credentials.ExecutablePath);

      // 2. Check rate limiting
      var rateLimitStopwatch = Stopwatch.StartNew();
      var rateLimitResult = await CheckRateLimit(credentials.ExecutablePath);
      _logger.LogInformation(
        "IPC rate-limit check completed in {ElapsedMs} ms for PID {ProcessId}.",
        rateLimitStopwatch.ElapsedMilliseconds,
        credentials.ProcessId);

      if (!rateLimitResult.IsSuccess)
      {
        _logger.LogCritical(
          "IPC authentication rate limit exceeded after {ElapsedMs} ms for {ExecutablePath}. PID: {ProcessId}",
          authenticationStopwatch.ElapsedMilliseconds,
          credentials.ExecutablePath,
          credentials.ProcessId);
        return Result.Fail<IpcClientCredentials>(rateLimitResult.Reason);
      }

      // 3. Validate executable path matches expected location
      var pathValidationStopwatch = Stopwatch.StartNew();
      var validationResult = ValidateExecutablePath(credentials.ExecutablePath);
      _logger.LogInformation(
        "IPC executable path validation completed in {ElapsedMs} ms for PID {ProcessId}.",
        pathValidationStopwatch.ElapsedMilliseconds,
        credentials.ProcessId);

      if (!validationResult.IsSuccess)
      {
        await RecordFailedAttempt(credentials.ExecutablePath);
        _logger.LogCritical(
          "IPC authentication FAILED for PID {ProcessId}, Path: {ExecutablePath} after {ElapsedMs} ms. Reason: {Reason}",
          credentials.ProcessId,
          credentials.ExecutablePath,
          authenticationStopwatch.ElapsedMilliseconds,
          validationResult.Reason);
        return Result.Fail<IpcClientCredentials>(validationResult.Reason);
      }

      // 4. Validate code signing certificate.
      var fileVerificationStopwatch = Stopwatch.StartNew();
      var certificateValidationResult = _fileVerifier.VerifyFile(credentials.ExecutablePath);
      _logger.LogInformation(
        "IPC file verification completed in {ElapsedMs} ms for PID {ProcessId}.",
        fileVerificationStopwatch.ElapsedMilliseconds,
        credentials.ProcessId);

      if (!certificateValidationResult.IsSuccess)
      {
        await RecordFailedAttempt(credentials.ExecutablePath);
        _logger.LogCritical(
          "IPC authentication FAILED for PID {ProcessId}, Path: {ExecutablePath} after {ElapsedMs} ms. Reason: {Reason}",
          credentials.ProcessId,
          credentials.ExecutablePath,
          authenticationStopwatch.ElapsedMilliseconds,
          certificateValidationResult.Reason);
        return Result.Fail<IpcClientCredentials>(certificateValidationResult.Reason);
      }

      // 5. Log successful authentication
      _logger.LogInformation(
        "IPC connection authenticated successfully in {ElapsedMs} ms. PID: {ProcessId}, Path: {ExecutablePath}",
        authenticationStopwatch.ElapsedMilliseconds,
        credentials.ProcessId,
        credentials.ExecutablePath);

      return credsResult;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Unexpected error during IPC authentication after {ElapsedMs} ms.", authenticationStopwatch.ElapsedMilliseconds);
      return Result.Fail<IpcClientCredentials>($"Unexpected error during authentication: {ex.Message}");
    }
  }

  public async Task<Result> CheckRateLimit(string executablePath)
  {
    try
    {
      await _rateLimitLock.WaitAsync();

      // Null or empty paths are always allowed (no rate limiting)
      if (string.IsNullOrWhiteSpace(executablePath))
      {
        return Result.Ok();
      }

      if (!_failedAttempts.TryGetValue(executablePath, out var attempts))
      {
        return Result.Ok();
      }

      // Remove attempts older than 1 minute
      var cutoff = _timeProvider.GetUtcNow().AddMinutes(-1);
      attempts.RemoveAll(x => x < cutoff);

      if (attempts.Count >= MaxFailuresPerMinute)
      {
        _logger.LogCritical(
          "Rate limit exceeded for {ExecutablePath}. {Count} failed attempts in the last minute.",
          executablePath,
          attempts.Count);
        return Result.Fail("Rate limit exceeded. Too many failed authentication attempts.");
      }

      return Result.Ok();
    }
    finally
    {
      _rateLimitLock.Release();
    }
  }

  public async Task RecordFailedAttempt(string executablePath)
  {
    try
    {
      await _rateLimitLock.WaitAsync();

      // Null or empty paths are ignored
      if (string.IsNullOrWhiteSpace(executablePath))
      {
        return;
      }

      _failedAttempts.AddOrUpdate(
        executablePath,
        _ => [_timeProvider.GetUtcNow()],
        (_, list) =>
        {
          list.Add(_timeProvider.GetUtcNow());
          return list;
        });
    }
    finally
    {
      _rateLimitLock.Release();
    }
  }

  private Result ValidateExecutablePath(string executablePath)
  {
    if (string.IsNullOrWhiteSpace(executablePath))
    {
      return Result.Fail("Executable path is null or empty.");
    }

    if (_systemEnvironment.IsDebug)
    {
      // Debug mode validation
      return ValidateExecutablePathDebug(executablePath);
    }

    // Release mode validation
    return ValidateExecutablePathRelease(executablePath);
  }

  private Result ValidateExecutablePathDebug(string executablePath)
  {
    // In debug mode, the actual process is dotnet.exe (the .NET runtime host)
    // We need to accept dotnet.exe as a valid executable
    var executableName = Path.GetFileName(executablePath);

    if (executableName.Equals("dotnet.exe", StringComparison.OrdinalIgnoreCase) ||
        executableName.Equals("dotnet", StringComparison.OrdinalIgnoreCase))
    {
      _logger.LogInformation("Debug mode: Accepting dotnet.exe runtime host as valid IPC client.");
      return Result.Ok();
    }

    // Also accept the actual DesktopClient executable if running directly
    var solutionDirResult = IoHelper.GetSolutionDir(Environment.CurrentDirectory);
    if (!solutionDirResult.IsSuccess)
    {
      _logger.LogWarning("Could not locate solution directory for debug mode validation.");
      return Result.Fail("Could not locate solution directory for debug mode path validation.");
    }

    var debugPath = Path.Combine(
      solutionDirResult.Value,
      "ControlR.DesktopClient",
      "bin",
      "Debug");

    var isInDebugPath = executablePath.StartsWith(debugPath, StringComparison.OrdinalIgnoreCase);
    var isCorrectExecutable = executableName.Equals(
      AppConstants.DesktopClientFileName,
      StringComparison.OrdinalIgnoreCase);

    if (isInDebugPath && isCorrectExecutable)
    {
      return Result.Ok();
    }

    return Result.Fail(
      $"Debug mode: Executable path '{executablePath}' must be dotnet.exe or '{AppConstants.DesktopClientFileName}' within '{debugPath}'.");
  }

  private Result ValidateExecutablePathRelease(string executablePath)
  {
    var expectedPath = _pathProvider.GetDesktopExecutablePath();
    if (executablePath.Equals(expectedPath, StringComparison.OrdinalIgnoreCase))
    {
      return Result.Ok();
    }

    return Result.Fail(
      $"Executable path '{executablePath}' does not match expected path '{expectedPath}'.");
  }
}