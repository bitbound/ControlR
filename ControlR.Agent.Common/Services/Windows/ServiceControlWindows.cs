using System.Runtime.Versioning;
using System.ServiceProcess;
using ControlR.Agent.Common.Interfaces;
using Microsoft.Extensions.Options;

namespace ControlR.Agent.Common.Services.Windows;

[SupportedOSPlatform("windows")]
internal class ServiceControlWindows(
    IOptions<InstanceOptions> instanceOptions,
    ILogger<ServiceControlWindows> logger) : IServiceControl
{
  private static readonly TimeSpan _serviceStatusTimeout = TimeSpan.FromSeconds(10);
  private readonly IOptions<InstanceOptions> _instanceOptions = instanceOptions;
  private readonly ILogger<ServiceControlWindows> _logger = logger;

  public async Task StartAgentService(bool throwOnFailure)
  {
    try
    {
      var serviceName = GetAgentServiceName();
      _logger.LogInformation("Starting agent service: {ServiceName}", serviceName);

      using var serviceController = new ServiceController(serviceName);

      if (serviceController.Status == ServiceControllerStatus.Running)
      {
        _logger.LogInformation("Agent service is already running.");
        return;
      }

      serviceController.Start();
      await WaitForStatusAsync(serviceController, ServiceControllerStatus.Running);
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

      using var serviceController = new ServiceController(serviceName);

      if (serviceController.Status == ServiceControllerStatus.Stopped)
      {
        _logger.LogInformation("Agent service is already stopped.");
        return;
      }

      serviceController.Stop();
      await WaitForStatusAsync(serviceController, ServiceControllerStatus.Stopped);
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

  public Task StartDesktopClientService(bool throwOnFailure)
  {
    throw new NotSupportedException("Desktop client service control is not supported on Windows. The desktop client is handled differently on this platform.");
  }

  public Task StopDesktopClientService(bool throwOnFailure)
  {
    throw new NotSupportedException("Desktop client service control is not supported on Windows. The desktop client is handled differently on this platform.");
  }

  private string GetAgentServiceName()
  {
    if (string.IsNullOrWhiteSpace(_instanceOptions.Value.InstanceId))
    {
      return "ControlR.Agent";
    }

    return $"ControlR.Agent ({_instanceOptions.Value.InstanceId})";
  }

  private static async Task WaitForStatusAsync(
    ServiceController serviceController,
    ServiceControllerStatus status)
  {
    await Task.Run(() =>
    {
      serviceController.WaitForStatus(status, _serviceStatusTimeout);  
    });
  }
}
