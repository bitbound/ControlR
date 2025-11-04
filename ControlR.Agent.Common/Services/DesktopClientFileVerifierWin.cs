using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using ControlR.Agent.Common.Interfaces;
using ControlR.Libraries.Shared.Helpers;

namespace ControlR.Agent.Common.Services;

public class DesktopClientFileVerifierWin(
  ISystemEnvironment systemEnvironment,
  ILogger<DesktopClientFileVerifierWin> logger) : IDesktopClientFileVerifier
{
  private readonly ISystemEnvironment _systemEnvironment = systemEnvironment;
  private readonly ILogger<DesktopClientFileVerifierWin> _logger = logger;

  public Result VerifyFile(string executablePath)
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

  private X509Certificate2? GetCodeSigningCertificate(string executablePath)
  {
    try
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
    catch (CryptographicException ex)
    {
      _logger.LogInformation(ex, "No certificate found.");
      return null;
    }
  }
}
