using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using ControlR.Agent.Common.Interfaces;
using ControlR.Libraries.Ipc;
using ControlR.Libraries.Shared.Constants;
using ControlR.Libraries.Shared.Helpers;

namespace ControlR.Agent.Common.Services;

public interface IIpcClientAuthenticator
{
  Task<Result> AuthenticateConnection(IIpcServer server);
  Task<Result> CheckRateLimit(string executablePath);
  Task RecordFailedAttempt(string executablePath);
}

public class IpcClientAuthenticator(
  TimeProvider timeProvider,
  IClientCredentialsProvider credentialsProvider,
  ISystemEnvironment systemEnvironment,
  ILogger<IpcClientAuthenticator> logger) : IIpcClientAuthenticator
{
  private const int MaxFailuresPerMinute = 5;

  private readonly IClientCredentialsProvider _credentialsProvider = credentialsProvider;
  private readonly ConcurrentDictionary<string, List<DateTimeOffset>> _failedAttempts = new();
  private readonly ILogger<IpcClientAuthenticator> _logger = logger;
  private readonly SemaphoreSlim _rateLimitLock = new(1, 1);
  private readonly ISystemEnvironment _systemEnvironment = systemEnvironment;
  private readonly TimeProvider _timeProvider = timeProvider;

  public async Task<Result> AuthenticateConnection(IIpcServer server)
  {
    try
    {
      // 1. Get client credentials from pipe/socket handle
      var credsResult = _credentialsProvider.GetClientCredentials(server);
      if (!credsResult.IsSuccess)
      {
        _logger.LogCritical(
          "Failed to get IPC client credentials: {Reason}",
          credsResult.Reason);
        return Result.Fail(credsResult.Reason);
      }

      var credentials = credsResult.Value;

      _logger.LogInformation(
        "IPC connection attempt from PID {ProcessId}, Path: {ExecutablePath}",
        credentials.ProcessId,
        credentials.ExecutablePath);

      // 2. Check rate limiting
      var rateLimitResult = await CheckRateLimit(credentials.ExecutablePath);
      if (!rateLimitResult.IsSuccess)
      {
        _logger.LogCritical(
          "IPC authentication rate limit exceeded for {ExecutablePath}. PID: {ProcessId}",
          credentials.ExecutablePath,
          credentials.ProcessId);
        return rateLimitResult;
      }

      // 3. Validate executable path matches expected location
      var validationResult = ValidateExecutablePath(credentials.ExecutablePath);
      if (!validationResult.IsSuccess)
      {
        await RecordFailedAttempt(credentials.ExecutablePath);
        _logger.LogCritical(
          "IPC authentication FAILED for PID {ProcessId}, Path: {ExecutablePath}. Reason: {Reason}",
          credentials.ProcessId,
          credentials.ExecutablePath,
          validationResult.Reason);
        return validationResult;
      }

      // 4. Validate code signing certificate.
      var certificateValidationResult = ValidateCodeSigningCertificate(credentials.ExecutablePath);
      if (!certificateValidationResult.IsSuccess)
      {
        await RecordFailedAttempt(credentials.ExecutablePath);
        _logger.LogCritical(
          "IPC authentication FAILED for PID {ProcessId}, Path: {ExecutablePath}. Reason: {Reason}",
          credentials.ProcessId,
          credentials.ExecutablePath,
          certificateValidationResult.Reason);
        return certificateValidationResult;
      }

      // 5. Log successful authentication
      _logger.LogInformation(
        "IPC connection authenticated successfully. PID: {ProcessId}, Path: {ExecutablePath}",
        credentials.ProcessId,
        credentials.ExecutablePath);

      return Result.Ok();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Unexpected error during IPC authentication.");
      return Result.Fail($"Unexpected error during authentication: {ex.Message}");
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

  private X509Certificate2? GetCodeSigningCertificate(string executablePath)
  {
    _logger.LogInformation("Inspecting digital signature for file: {FilePath}", executablePath);
    if (X509Certificate2.GetCertContentType(executablePath) == X509ContentType.Authenticode)
    {
      _logger.LogInformation("Code signing certificate found.");
      // https://github.com/dotnet/runtime/discussions/108740
      // It appears they removed it without having a replacement because it "looked crufty" or something.
#pragma warning disable SYSLIB0057 // Type or member is obsolete
      return new X509Certificate2(executablePath);
#pragma warning restore SYSLIB0057 // Type or member is obsolete
    }
    _logger.LogInformation("No code signing certificate found.");
    return null;
  }

  private Result ValidateCodeSigningCertificate(string executablePath)
  {
    switch (_systemEnvironment.Platform)
    {
      case SystemPlatform.Windows:
        {
          return ValidateWindowsCodeSignature(executablePath);
        }
      case SystemPlatform.MacOs:
        {
          return Result.Ok();
        }
      case SystemPlatform.Linux:
        {
          return Result.Ok();
        }
    }

    return Result.Fail("Code signing certificate validation not implemented for this OS.");
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
    // Production/Release mode: strict validation
    var expectedPath = AppConstants.GetDesktopExecutablePath(_systemEnvironment.StartupDirectory);

    if (executablePath.Equals(expectedPath, StringComparison.OrdinalIgnoreCase))
    {
      return Result.Ok();
    }

    return Result.Fail(
      $"Executable path '{executablePath}' does not match expected path '{expectedPath}'.");
  }

  private Result ValidateWindowsCodeSignature(string executablePath)
  {
    try
    {
      var agentExePath = _systemEnvironment.StartupExePath;

      // Get certificate from the agent executable
      var agentCertificate = GetCodeSigningCertificate(agentExePath);

      // If agent is not signed, skip certificate validation
      if (agentCertificate is null)
      {
        _logger.LogInformation(
          "Agent executable is not code signed. Skipping certificate validation for IPC client.");
        return Result.Ok();
      }

      // Get certificate from the client executable
      var clientCertificate = GetCodeSigningCertificate(executablePath);

      if (clientCertificate is null)
      {
        return Result.Fail(
          $"Client executable '{executablePath}' is not code signed, but agent executable is signed. " +
          "Both must be signed with the same certificate.");
      }

      // Compare certificate thumbprints
      if (!agentCertificate.Thumbprint.Equals(clientCertificate.Thumbprint, StringComparison.OrdinalIgnoreCase))
      {
        _logger.LogCritical(
          "Certificate mismatch. Agent thumbprint: {AgentThumbprint}, Client thumbprint: {ClientThumbprint}",
          agentCertificate.Thumbprint,
          clientCertificate.Thumbprint);
        return Result.Fail(
          "Client executable is not signed with the same certificate as the agent executable.");
      }

      _logger.LogInformation(
        "Code signing certificate validation passed. Certificate thumbprint: {Thumbprint}",
        agentCertificate.Thumbprint);

      return Result.Ok();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error validating Windows code signing certificate.");
      return Result.Fail($"Error validating code signing certificate: {ex.Message}");
    }
  }
}