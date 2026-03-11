using System.Diagnostics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using ControlR.Agent.Common.Interfaces;
using ControlR.Libraries.Shared.Services.FileSystem;
using Microsoft.Security.Extensions;

namespace ControlR.Agent.Common.Services;

public class DesktopClientFileVerifierWin(
  ISystemEnvironment systemEnvironment,
  IFileSystem fileSystem,
  ILogger<DesktopClientFileVerifierWin> logger) : IDesktopClientFileVerifier
{
  private readonly IFileSystem _fileSystem = fileSystem;
  private readonly ILogger<DesktopClientFileVerifierWin> _logger = logger;
  private readonly ISystemEnvironment _systemEnvironment = systemEnvironment;

  public Result VerifyFile(string executablePath)
  {
    var verificationStopwatch = Stopwatch.StartNew();

    try
    {
      var agentExePath = _systemEnvironment.StartupExePath;

      // Get certificate from the agent executable
      var agentCertificate = GetCodeSigningCertificate(agentExePath);

      // If agent is not signed, skip certificate validation
      if (agentCertificate is null)
      {
        _logger.LogInformation(
          "Agent executable is not code signed. Skipping certificate validation for IPC client. Total verification time: {ElapsedMs} ms.",
          verificationStopwatch.ElapsedMilliseconds);
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
        "Code signing certificate validation passed in {ElapsedMs} ms. Certificate thumbprint: {Thumbprint}",
        verificationStopwatch.ElapsedMilliseconds,
        agentCertificate.Thumbprint);

      return Result.Ok();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error validating Windows code signing certificate after {ElapsedMs} ms.", verificationStopwatch.ElapsedMilliseconds);
      return Result.Fail($"Error validating code signing certificate: {ex.Message}");
    }
  }

  private X509Certificate2? GetCodeSigningCertificate(string executablePath)
  {
    var inspectionStopwatch = Stopwatch.StartNew();

    try
    {
      _logger.LogInformation("Inspecting digital signature for file: {FilePath}", executablePath);
      if (X509Certificate2.GetCertContentType(executablePath) == X509ContentType.Authenticode)
      {
        _logger.LogInformation("Code signing certificate found. File: {FilePath}", executablePath);
        using var fs = _fileSystem.CreateFileStream(executablePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (fs is not FileStream fileStream)
        {
          throw new InvalidOperationException(
            $"Expected a FileStream for path '{executablePath}', but got {fs.GetType().FullName}.");
        }
        var signatureInfo = FileSignatureInfo.GetFromFileStream(fileStream);
        _logger.LogInformation(
          "Digital signature inspection completed in {ElapsedMs} ms for file: {FilePath}",
          inspectionStopwatch.ElapsedMilliseconds,
          executablePath);
        return signatureInfo.SigningCertificate;
      }
      _logger.LogInformation(
        "No code signing certificate found after {ElapsedMs} ms. File: {FilePath}",
        inspectionStopwatch.ElapsedMilliseconds,
        executablePath);
      return null;
    }
    catch (CryptographicException ex)
    {
      _logger.LogInformation(ex, "No certificate found after {ElapsedMs} ms. File: {FilePath}", inspectionStopwatch.ElapsedMilliseconds, executablePath);
      return null;
    }
  }
}
