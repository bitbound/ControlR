using ControlR.ApiClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ControlR.ApiClientExample;

internal class ExampleService(
  IServiceProvider serviceProvider,
  IHostApplicationLifetime hostLifetime,
  IOptionsMonitor<ControlrApiClientOptions> optionsMonitor,
  ILogger<ExampleService> logger): BackgroundService
{
  private readonly IHostApplicationLifetime _hostLifetime = hostLifetime;
  private readonly ILogger<ExampleService> _logger = logger;
  private readonly IOptionsMonitor<ControlrApiClientOptions> _optionsMonitor = optionsMonitor;
  private readonly IServiceProvider _serviceProvider = serviceProvider;

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    try
    {
      _hostLifetime.ApplicationStarted.WaitHandle.WaitOne(TimeSpan.FromSeconds(5));
      await UseDependencyInjection(stoppingToken);
      await UseStaticBuilder(stoppingToken);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error occurred while invoking example calls.");
    }
    finally
    {
      _logger.LogInformation("Example calls finished.  Press Ctrl + C to close.");
    }
  }

  private async Task RetrieveDevices(IControlrApi client, CancellationToken cancellationToken)
  {
    var deviceDetails = new List<string>();
    var deviceCount = 0;
    await foreach (var device in client.Devices.GetAllDevices(cancellationToken).WithCancellation(cancellationToken))
    {
      deviceCount++;
      deviceDetails.Add($"\t- {device.Name} (ID: {device.Id})");
    }

    _logger.LogInformation("Retrieved {DeviceCount} devices.", deviceCount);
    if (deviceCount > 0)
    {
      _logger.LogInformation("Device details:\n{DeviceDetails}", string.Join("\n", deviceDetails));
    }
  }

  private async Task UseDependencyInjection(CancellationToken cancellationToken)
  {
    _logger.LogInformation("Retrieving devices using IControlrApiClientFactory.");

    var client = _serviceProvider.GetRequiredService<IControlrApi>();
    await RetrieveDevices(client, cancellationToken);
  }

  private async Task UseStaticBuilder(CancellationToken cancellationToken)
  {
    _logger.LogInformation("Retrieving devices using ControlrApiClientBuilder.");

    ControlrApiClientBuilder.Initialize(options =>
    {
      options.BaseUrl = _optionsMonitor.CurrentValue.BaseUrl;
      options.PersonalAccessToken = _optionsMonitor.CurrentValue.PersonalAccessToken;
    });

    var client = ControlrApiClientBuilder.GetClient();
    await RetrieveDevices(client, cancellationToken);
  }
}
