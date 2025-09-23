using ControlR.Agent.Common.Interfaces;
using ControlR.Libraries.DevicesCommon.Services.Processes;
using Microsoft.Extensions.Options;

namespace ControlR.Agent.Common.Services.Linux;

internal class ServiceControlLinux(
    IProcessManager processManager,
    IHeadlessServerDetector headlessServerDetector,
    IOptions<InstanceOptions> instanceOptions,
    ILogger<ServiceControlLinux> logger) : IServiceControl
{
    private static readonly TimeSpan _serviceStatusTimeout = TimeSpan.FromSeconds(10);
    private readonly IHeadlessServerDetector _headlessServerDetector = headlessServerDetector;
    private readonly IOptions<InstanceOptions> _instanceOptions = instanceOptions;
    private readonly ILogger<ServiceControlLinux> _logger = logger;
    private readonly IProcessManager _processManager = processManager;

    public async Task StartAgentService(bool throwOnFailure)
    {
        try
        {
            var serviceName = GetAgentServiceName();
            _logger.LogInformation("Starting agent service: {ServiceName}", serviceName);

            await _processManager.StartAndWaitForExit("sudo", $"systemctl start {serviceName}", false, _serviceStatusTimeout);

            _logger.LogInformation("Agent service started successfully.");
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
        // Check if running on headless server
        var isHeadless = await _headlessServerDetector.IsHeadlessServer();
        if (isHeadless)
        {
            _logger.LogInformation("Running on headless Ubuntu server. Desktop client service is not applicable.");
            return;
        }

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
                tasks.Add(StartDesktopClientForUser(uid, serviceName, throwOnFailure));
            }

            await Task.WhenAll(tasks);
            _logger.LogInformation("Desktop client service started for all logged-in users.");
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
            var serviceName = GetAgentServiceName();
            _logger.LogInformation("Stopping agent service: {ServiceName}", serviceName);

            await _processManager.StartAndWaitForExit("sudo", $"systemctl stop {serviceName}", false, _serviceStatusTimeout);

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
        // Check if running on headless server
        var isHeadless = await _headlessServerDetector.IsHeadlessServer();
        if (isHeadless)
        {
            _logger.LogInformation("Running on headless Ubuntu server. Desktop client service is not applicable.");
            return;
        }

        try
        {
            var serviceName = GetDesktopClientServiceName();
            _logger.LogInformation("Stopping desktop client service for all logged-in users: {ServiceName}", serviceName);

            var loggedInUsers = await GetLoggedInUsers();
            if (loggedInUsers.Count == 0)
            {
                _logger.LogInformation("No logged-in users found. Nothing to stop.");
                return;
            }

            var tasks = new List<Task>();
            foreach (var uid in loggedInUsers)
            {
                tasks.Add(StopDesktopClientForUser(uid, serviceName, throwOnFailure));
            }

            await Task.WhenAll(tasks);
            _logger.LogInformation("Desktop client service stopped for all logged-in users.");
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

    private string GetAgentServiceName()
    {
        if (string.IsNullOrWhiteSpace(_instanceOptions.Value.InstanceId))
        {
            return "controlr.agent.service";
        }

        return $"controlr.agent-{_instanceOptions.Value.InstanceId}.service";
    }

    private string GetDesktopClientServiceName()
    {
        if (string.IsNullOrWhiteSpace(_instanceOptions.Value.InstanceId))
        {
            return "controlr.desktop.service";
        }

        return $"controlr.desktop-{_instanceOptions.Value.InstanceId}.service";
    }

    private async Task<List<string>> GetLoggedInUsers()
    {
        try
        {
            // Get currently logged-in users using 'who' command
            var result = await _processManager.GetProcessOutput("who", "", 5000);

            if (!result.IsSuccess || string.IsNullOrWhiteSpace(result.Value))
            {
                _logger.LogWarning("Failed to get logged-in users: {Reason}", result.Reason);
                return [];
            }

            var lines = result.Value.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var users = new HashSet<string>();

            foreach (var line in lines)
            {
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 0)
                {
                    var username = parts[0];
                    // Get the UID for this username
                    var idResult = await _processManager.GetProcessOutput("id", $"-u {username}", 3000);
                    if (idResult.IsSuccess && !string.IsNullOrWhiteSpace(idResult.Value))
                    {
                        var uid = idResult.Value.Trim();
                        users.Add(uid);
                    }
                }
            }

            return users.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while getting logged-in users.");
            return [];
        }
    }

    private async Task StartDesktopClientForUser(string uid, string serviceName, bool throwOnFailure)
    {
        try
        {
            _logger.LogInformation("Starting desktop client service for user {UID}: {ServiceName}", uid, serviceName);

            // Use sudo to run as the specific user with proper XDG_RUNTIME_DIR
            await _processManager.StartAndWaitForExit("sudo",
                $"-u #{uid} XDG_RUNTIME_DIR=/run/user/{uid} systemctl --user start {serviceName}",
                false, _serviceStatusTimeout);

            _logger.LogInformation("Desktop client service started for user {UID}.", uid);
        }
        catch (Exception ex)
        {
            if (throwOnFailure)
            {
                _logger.LogError(ex, "Failed to start desktop client service for user {UID}.", uid);
                throw;
            }
            else
            {
                _logger.LogWarning(ex, "Failed to start desktop client service for user {UID}.", uid);
            }
        }
    }

    private async Task StopDesktopClientForUser(string uid, string serviceName, bool throwOnFailure)
    {
        try
        {
            _logger.LogInformation("Stopping desktop client service for user {UID}: {ServiceName}", uid, serviceName);

            // Use sudo to run as the specific user with proper XDG_RUNTIME_DIR
            await _processManager.StartAndWaitForExit("sudo",
                $"-u #{uid} XDG_RUNTIME_DIR=/run/user/{uid} systemctl --user stop {serviceName}",
                false, _serviceStatusTimeout);

            _logger.LogInformation("Desktop client service stopped for user {UID}.", uid);
        }
        catch (Exception ex)
        {
            if (throwOnFailure)
            {
                _logger.LogError(ex, "Failed to stop desktop client service for user {UID}.", uid);
                throw;
            }
            else
            {
                _logger.LogWarning(ex, "Failed to stop desktop client service for user {UID}.", uid);
            }
        }
    }
}
