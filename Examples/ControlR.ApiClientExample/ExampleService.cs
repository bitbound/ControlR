using ControlR.ApiClient;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Threading;

namespace ControlR.ApiClientExample;

internal class ExampleService(
  IControlrApiClientFactory clientFactory,
  IHostApplicationLifetime hostLifetime,
  IOptionsMonitor<ControlrApiClientOptions> optionsMonitor,
  ILogger<ExampleService> logger): BackgroundService
{
  private readonly IControlrApiClientFactory _clientFactory = clientFactory;
  private readonly IHostApplicationLifetime _hostLifetime = hostLifetime;
  private readonly ILogger<ExampleService> _logger = logger;
  private readonly IOptionsMonitor<ControlrApiClientOptions> _optionsMonitor = optionsMonitor;

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

  private async Task RetrieveDevices(ControlrApiClient client, CancellationToken cancellationToken)
  {
    var devices = await client.Api.Devices.GetAsync(cancellationToken: cancellationToken);

    if (devices is null)
    {
      _logger.LogError("Response was empty.");
      return;
    }

    _logger.LogInformation("Retrieved {DeviceCount} devices.", devices.Count);
    if (devices.Count > 0)
    {
      var details = string.Join("\n", devices.Select(d => $"\t- {d.Name} (ID: {d.Id})"));
      _logger.LogInformation("Device details:\n{DeviceDetails}", details);
    }
  }

  private async Task UseDependencyInjection(CancellationToken cancellationToken)
  {
    _logger.LogInformation("Retrieving devices using IControlrApiClientFactory.");

    var client = _clientFactory.GetClient();
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
