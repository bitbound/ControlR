using ControlR.Agent.Shared.Options;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi;
using ControlR.Libraries.Shared.Services.Encryption;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Hosting;

namespace ControlR.Agent.Common.Services;

internal interface IAgentHeartbeatTimer : IHostedService
{
  Task SendDeviceHeartbeat();
}

internal class AgentHeartbeatTimer(
  TimeProvider timeProvider,
  IHubConnection<IAgentHub> hubConnection,
  ISystemEnvironment systemEnvironment,
  IDeviceInfoProvider deviceDataGenerator,
  IOptionsAccessor optionsAccessor,
  IEd25519KeyProvider keyProvider,
  ILogger<AgentHeartbeatTimer> logger) : BackgroundService, IAgentHeartbeatTimer
{
  private readonly IDeviceInfoProvider _deviceDataGenerator = deviceDataGenerator;
  private readonly IHubConnection<IAgentHub> _hubConnection = hubConnection;
  private readonly IEd25519KeyProvider _keyProvider = keyProvider;
  private readonly ILogger<AgentHeartbeatTimer> _logger = logger;
  private readonly IOptionsAccessor _optionsAccessor = optionsAccessor;
  private readonly ISystemEnvironment _systemEnvironment = systemEnvironment;
  private readonly TimeProvider _timeProvider = timeProvider;
  
  public async Task SendDeviceHeartbeat()
  {
    try
    {
      using var _ = _logger.BeginMemberScope();

      if (_hubConnection.ConnectionState != HubConnectionState.Connected)
      {
        _logger.LogWarning("Not connected to hub when trying to send device update.");
        return;
      }

      var device = await _deviceDataGenerator.GetDeviceInfo();

      var dto = device.CloneAs<DeviceUpdateRequestDto>();

      var privateKeyBase64 = _optionsAccessor.PrivateKey;
      string? publicKeyBase64 = null;
      
      if (string.IsNullOrWhiteSpace(privateKeyBase64))
      {
        _logger.LogInformation("No private key found. Generating new Ed25519 keypair for identity bootstrapping.");
        var keyPair = _keyProvider.GenerateKeyPair();
        privateKeyBase64 = Convert.ToBase64String(keyPair.PrivateKey);
        publicKeyBase64 = Convert.ToBase64String(keyPair.PublicKey);

        await _optionsAccessor.UpdatePrivateKey(privateKeyBase64);

        _logger.LogInformation("New keypair generated and saved.");
      }
      else
      {
        _logger.LogDebug("Private key found. Using existing key for heartbeat.");
        publicKeyBase64 = _keyProvider.DerivePublicKeyBase64(privateKeyBase64);
      }

      var privateKey = Convert.FromBase64String(privateKeyBase64);
      var signedDto = _keyProvider.Sign(dto, privateKey, publicKeyBase64);
      var updateResult = await _hubConnection.Server.UpdateDeviceSigned(signedDto);

      if (!updateResult.IsSuccess)
      {
        _logger.LogResult(updateResult);
        return;
      }

      if (updateResult.Value.Id != device.Id)
      {
        _logger.LogInformation("Device ID changed.  Updating appsettings.");
        await _optionsAccessor.UpdateId(updateResult.Value.Id);
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while sending device update.");
    }
  }


  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    var delayTime = _systemEnvironment.IsDebug ?
        TimeSpan.FromSeconds(10) :
        TimeSpan.FromMinutes(5);

    using var timer = new PeriodicTimer(delayTime, _timeProvider);
    try
    {
      while (await timer.WaitForNextTickAsync(stoppingToken))
      {
        try
        {
          await SendDeviceHeartbeat();
        }
        catch (Exception ex)
        {
          _logger.LogError(ex, "Error while sending agent heartbeat.");
        }
      }
    }
    catch (OperationCanceledException)
    {
      _logger.LogInformation("Heartbeat aborted.  Application shutting down.");
    }
  }
}
