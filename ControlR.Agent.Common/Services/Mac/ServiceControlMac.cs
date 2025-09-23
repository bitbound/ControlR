using System.Diagnostics;
using ControlR.Agent.Common.Interfaces;
using ControlR.Libraries.DevicesCommon.Services.Processes;
using Microsoft.Extensions.Options;

namespace ControlR.Agent.Common.Services.Mac;

internal class ServiceControlMac(
    IProcessManager processManager,
    IOptions<InstanceOptions> instanceOptions,
    ILogger<ServiceControlMac> logger) : IServiceControl
{
  private static readonly TimeSpan _serviceStatusTimeout = TimeSpan.FromSeconds(20);
  private readonly IOptions<InstanceOptions> _instanceOptions = instanceOptions;
  private readonly ILogger<ServiceControlMac> _logger = logger;
  private readonly IProcessManager _processManager = processManager;
  public async Task StartAgentService(bool throwOnFailure)
  {
    try
    {
      var serviceName = GetAgentServiceName();
      _logger.LogInformation("Starting agent service: {ServiceName}", serviceName);

      var psi = GetDefaultPsi();
      try
      {
        psi.Arguments = $"launchctl bootstrap system {GetAgentPlistPath()}";
        await _processManager.StartAndWaitForExit(psi, _serviceStatusTimeout);
      }
      catch
      {
        // Might fail if already loaded.
      }

      psi.Arguments = $"launchctl kickstart -k system/{serviceName}";
      await _processManager.StartAndWaitForExit(psi, _serviceStatusTimeout);

      _logger.LogInformation("Agent service started successfully.");
    }
    catch (OperationCanceledException)
    {
      _logger.LogWarning("Timed out while waiting for agent to start.");
      return;
    }
    catch (Exception ex)
    {
      if (throwOnFailure)
      {
        _logger.LogError(ex, "Failed to start agent service.");
        throw;
      }
      else
      {
        _logger.LogInformation(ex, "Failed to start agent service.");
      }
    }
  }

  public async Task StartDesktopClientService(bool throwOnFailure)
  {
    try
    {
      var serviceName = GetDesktopClientServiceName();
      _logger.LogInformation("Starting desktop client service for all logged-in users: {ServiceName}", serviceName);

      var loggedInUsers = await GetLoggedInUsers();
      if (loggedInUsers.Count == 0)
      {
        _logger.LogWarning("No logged-in users found. Desktop client service will not be started.");
        return;
      }

      var tasks = new List<Task>();
      foreach (var uid in loggedInUsers)
      {
        tasks.Add(StartDesktopClientForUser(uid, serviceName));
      }

      await Task.WhenAll(tasks);
      _logger.LogInformation("Desktop client service started successfully for {UserCount} users.", loggedInUsers.Count);
    }
    catch (Exception ex)
    {
      if (throwOnFailure)
      {
        _logger.LogError(ex, "Failed to start desktop client service.");
        throw;
      }
      else
      {
        _logger.LogInformation(ex, "Failed to start desktop client service.");
      }
    }
  }

  public async Task StopAgentService(bool throwOnFailure)
  {
    try
    {
      var psi = GetDefaultPsi();
      var serviceName = GetAgentServiceName();
      _logger.LogInformation("Stopping agent service: {ServiceName}", serviceName);

      psi.Arguments = $"launchctl bootout system {GetAgentPlistPath()}";
      await _processManager.StartAndWaitForExit(psi, _serviceStatusTimeout);

      _logger.LogInformation("Agent service stopped successfully.");
    }
    catch (Exception ex)
    {
      if (throwOnFailure)
      {
        _logger.LogError(ex, "Failed to stop agent service.");
        throw;
      }
      else
      {
        _logger.LogInformation(ex, "Failed to stop agent service.");
      }
    }
  }

  public async Task StopDesktopClientService(bool throwOnFailure)
  {
    try
    {
      var serviceName = GetDesktopClientServiceName();
      _logger.LogInformation("Stopping desktop client service for all logged-in users: {ServiceName}", serviceName);

      var loggedInUsers = await GetLoggedInUsers();
      if (loggedInUsers.Count == 0)
      {
        _logger.LogWarning("No logged-in users found. Desktop client service stop operation completed.");
        return;
      }

      var tasks = loggedInUsers.Select(x => StopDesktopClientForUser(x, serviceName));

      await Task.WhenAll(tasks);
      _logger.LogInformation("Desktop client service stopped successfully for {UserCount} users.", loggedInUsers.Count);
    }
    catch (Exception ex)
    {
      if (throwOnFailure)
      {
        _logger.LogError(ex, "Failed to stop desktop client service.");
        throw;
      }
      else
      {
        _logger.LogInformation(ex, "Failed to stop desktop client service.");
      }
    }
  }

  private static ProcessStartInfo GetDefaultPsi()
  {
    return new ProcessStartInfo
    {
      FileName = "sudo",
      WorkingDirectory = "/tmp",
      UseShellExecute = true
    };
  }

  private string GetAgentPlistPath()
  {
    if (string.IsNullOrWhiteSpace(_instanceOptions.Value.InstanceId))
    {
      return "/Library/LaunchDaemons/app.controlr.agent.plist";
    }

    return $"/Library/LaunchDaemons/app.controlr.agent.{_instanceOptions.Value.InstanceId}.plist";
  }

  private string GetAgentServiceName()
  {
    if (string.IsNullOrWhiteSpace(_instanceOptions.Value.InstanceId))
    {
      return "app.controlr.agent";
    }

    return $"app.controlr.agent.{_instanceOptions.Value.InstanceId}";
  }

  private string GetDesktopClientPlistPath()
  {
    if (string.IsNullOrWhiteSpace(_instanceOptions.Value.InstanceId))
    {
      return "/Library/LaunchAgents/app.controlr.desktop.plist";
    }

    return $"/Library/LaunchAgents/app.controlr.desktop.{_instanceOptions.Value.InstanceId}.plist";
  }

  private string GetDesktopClientServiceName()
  {
    if (string.IsNullOrWhiteSpace(_instanceOptions.Value.InstanceId))
    {
      return "app.controlr.desktop";
    }

    return $"app.controlr.desktop.{_instanceOptions.Value.InstanceId}";
  }

  private async Task<List<string>> GetLoggedInUsers()
  {
    try
    {
      var result = await _processManager.GetProcessOutput("who", "-u", 5000);
      var users = new List<string>();

      if (result.IsSuccess && !string.IsNullOrWhiteSpace(result.Value))
      {
        var lines = result.Value.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
          // Parse the who output to extract usernames
          // Format: username console timestamp
          var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
          if (parts.Length >= 1)
          {
            var username = parts[0];
            // Get UID for the user
            var uidResult = await _processManager.GetProcessOutput("id", $"-u {username}", 3000);
            if (uidResult.IsSuccess &&
                !string.IsNullOrWhiteSpace(uidResult.Value) &&
                int.TryParse(uidResult.Value.Trim(), out var uid) &&
                uid >= 500) // Exclude system users (typically UID < 500)
            {
              users.Add(uidResult.Value.Trim());
            }
          }
        }
      }

      return [.. users.Distinct()];
    }
    catch (Exception ex)
    {
      _logger.LogWarning(ex, "Failed to get logged-in users. Falling back to empty list.");
      return [];
    }
  }

  private async Task StartDesktopClientForUser(string uid, string serviceName)
  {
    try
    {
      _logger.LogDebug("Starting desktop client service for user {UID}: {ServiceName}", uid, serviceName);

      var psi = GetDefaultPsi();
      psi.Arguments = $"launchctl bootstrap gui/{uid} {GetDesktopClientPlistPath()}";

      try
      {
        await _processManager.StartAndWaitForExit(psi, _serviceStatusTimeout);
      }
      catch
      {
        // Might fail if already loaded, which is okay
      }

      // Kickstart the service
      psi.Arguments = $"launchctl kickstart -k gui/{uid}/{serviceName}";
      await _processManager.StartAndWaitForExit(psi, _serviceStatusTimeout);

      _logger.LogDebug("Desktop client service started successfully for user {UID}.", uid);
    }
    catch (Exception ex)
    {
      _logger.LogWarning(ex, "Failed to start desktop client service for user {UID}.", uid);
      // Don't throw here as we want to continue with other users
    }
  }

  private async Task StopDesktopClientForUser(string uid, string serviceName)
  {
    try
    {
      _logger.LogDebug("Stopping desktop client service for user {UID}: {ServiceName}", uid, serviceName);

      var psi = GetDefaultPsi();
      psi.Arguments = $"launchctl bootout gui/{uid} {GetDesktopClientPlistPath()}";
      await _processManager.StartAndWaitForExit(psi, _serviceStatusTimeout);

      _logger.LogDebug("Desktop client service stopped successfully for user {UID}.", uid);
    }
    catch (Exception ex)
    {
      _logger.LogWarning(ex, "Failed to stop desktop client service for user {UID}.", uid);
      // Don't throw here as we want to continue with other users
    }
  }

}
