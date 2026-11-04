using System.Runtime.Versioning;
using ControlR.Libraries.Shared.Services.Processes;

namespace ControlR.Agent.Shared.Services.Windows;

/// <summary>
/// Where a service is registered to run from.
/// </summary>
/// <param name="ExecutablePath">The executable the registration names.</param>
/// <param name="RunArguments">Everything the registration passes to that executable.</param>
/// <param name="Directory">The directory holding <paramref name="ExecutablePath"/>.</param>
public sealed record ServiceRegistration(string ExecutablePath, string RunArguments, string Directory);

/// <summary>
/// The Windows service registration itself, read and rewritten without going through the service
/// control manager's start and stop paths.
/// </summary>
[SupportedOSPlatform("windows")]
public interface IAgentServiceRegistration
{
  /// <summary>
  /// Points an existing service at a new executable and its run arguments, leaving every other part of
  /// the registration, including its failure actions, as it is. Used instead of delete and create because
  /// a deleted service stays in pending-delete until its last handle closes, and a create issued in that
  /// window fails.
  /// </summary>
  Task<Result> Repoint(string serviceName, string agentExecutablePath, string runArguments);

  /// <summary>
  /// Returns where the named service is registered to run from, or null when no service of that name is
  /// registered or its registration cannot be read. This is the only machine state that records where an
  /// installed agent's files are, so a caller that has to act on an existing install cannot substitute the
  /// path formula it computes. The whole registration target comes back rather than only its directory, so
  /// a caller that changes it can also put it back exactly as it found it.
  /// </summary>
  ServiceRegistration? TryGetRegistration(string serviceName);
}

[SupportedOSPlatform("windows")]
internal sealed class AgentServiceRegistration(
  IProcessManager processes,
  IRegistryAccessor registryAccessor,
  ILogger<AgentServiceRegistration> logger) : IAgentServiceRegistration
{
  public async Task<Result> Repoint(string serviceName, string agentExecutablePath, string runArguments)
  {
    using var _ = logger.BeginMemberScope();

    logger.LogInformation("Repointing the {ServiceName} service at {AgentExecutablePath}.", serviceName, agentExecutablePath);

    var binPath = string.IsNullOrWhiteSpace(runArguments)
      ? $"\\\"{agentExecutablePath}\\\""
      : $"\\\"{agentExecutablePath}\\\" {runArguments}";
    var configString = $"sc.exe config \"{serviceName}\" binPath= \"{binPath}\"";
    var result = await processes.GetProcessOutput("cmd.exe", $"/c {configString}");
    if (!result.IsSuccess)
    {
      logger.LogError("Failed to repoint the {ServiceName} service. Reason: {Reason}", serviceName, result.Reason);
      return result.ToResult();
    }

    return Result.Ok();
  }

  public ServiceRegistration? TryGetRegistration(string serviceName)
  {
    var imagePath = registryAccessor.GetServiceImagePath(serviceName);
    if (string.IsNullOrWhiteSpace(imagePath))
    {
      return null;
    }

    var trimmed = imagePath.TrimStart();

    if (trimmed.StartsWith('"'))
    {
      var closingQuote = trimmed.IndexOf('"', 1);
      if (closingQuote <= 0)
      {
        return null;
      }

      return new ServiceRegistration(
        trimmed[1..closingQuote],
        trimmed[(closingQuote + 1)..].Trim(),
        GetDirectory(trimmed[1..closingQuote]));
    }

    var firstSpace = trimmed.IndexOf(' ');
    var executablePath = firstSpace > 0 ? trimmed[..firstSpace] : trimmed;
    var runArguments = firstSpace > 0 ? trimmed[(firstSpace + 1)..].Trim() : string.Empty;

    return new ServiceRegistration(executablePath, runArguments, GetDirectory(executablePath));
  }

  // Windows separators are searched by hand rather than through Path, because the code that reads a
  // registration written on Windows also runs on a build host where Path does not treat a backslash as
  // a separator.
  private static string GetDirectory(string filePath)
  {
    var trimmed = filePath.TrimEnd('\\');
    var lastSeparator = trimmed.LastIndexOf('\\');
    return lastSeparator <= 0 ? trimmed : trimmed[..lastSeparator];
  }
}
