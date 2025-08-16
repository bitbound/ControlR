using ControlR.Agent.Common.Interfaces;
using ControlR.Libraries.DevicesCommon.Options;
using ControlR.Libraries.DevicesCommon.Services.Processes;
using Microsoft.Extensions.Options;

namespace ControlR.Agent.Common.Services.Linux;

internal class ServiceControlLinux(
    IProcessManager processManager,
    IOptions<InstanceOptions> instanceOptions,
    ILogger<ServiceControlLinux> logger) : IServiceControl
{
    private static readonly TimeSpan _serviceStatusTimeout = TimeSpan.FromSeconds(10);
    private readonly IProcessManager _processManager = processManager;
    private readonly IOptions<InstanceOptions> _instanceOptions = instanceOptions;
    private readonly ILogger<ServiceControlLinux> _logger = logger;

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

    public async Task StartDesktopClientService(bool throwOnFailure)
    {
        try
        {
            var serviceName = GetDesktopClientServiceName();
            _logger.LogInformation("Starting desktop client service: {ServiceName}", serviceName);

            // Use systemctl --user for user services (equivalent to LaunchAgent)
            await _processManager.StartAndWaitForExit("systemctl", $"--user start {serviceName}", false, _serviceStatusTimeout);

            _logger.LogInformation("Desktop client service started successfully.");
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

    public async Task StopDesktopClientService(bool throwOnFailure)
    {
        try
        {
            var serviceName = GetDesktopClientServiceName();
            _logger.LogInformation("Stopping desktop client service: {ServiceName}", serviceName);

            // Use systemctl --user for user services (equivalent to LaunchAgent)
            await _processManager.StartAndWaitForExit("systemctl", $"--user stop {serviceName}", false, _serviceStatusTimeout);

            _logger.LogInformation("Desktop client service stopped successfully.");
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
}
