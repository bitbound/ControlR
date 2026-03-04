using Microsoft.AspNetCore.Http.Connections.Client;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Options;

namespace ControlR.Libraries.Viewer.Common.Services;

public interface IViewerHubConnector
{
  Task<bool> Connect(CancellationToken cancellationToken = default);
  Task Disconnect();
}

public class ViewerHubConnector(
  IHubConnection<IViewerHub> viewerHub,
  IMessenger messenger,
  IOptions<ControlrViewerOptions> options,
  ILogger<ViewerHubConnector> logger) : IViewerHubConnector
{
  private readonly ILogger<ViewerHubConnector> _logger = logger;
  private readonly IMessenger _messenger = messenger;
  private readonly ControlrViewerOptions _options = options.Value;
  private readonly IHubConnection<IViewerHub> _viewerHub = viewerHub;
  private bool _isConfigured;

  public async Task<bool> Connect(CancellationToken cancellationToken = default)
  {
    try
    {
      if (_viewerHub.IsConnected)
      {
        return true;
      }

      var hubUri = new Uri(_options.BaseUrl, AppConstants.ViewerHubPath);

      await _messenger.Send(new HubConnectionStateChangedMessage(HubConnectionState.Connecting));

      var result = await _viewerHub.Connect(
        hubEndpoint: hubUri,
        autoRetry: true,
        configure: ConfigureHttpOptions,
        cancellationToken: cancellationToken);

      if (!_isConfigured)
      {
        _viewerHub.Closed += async (ex) =>
        {
          await _messenger.Send(new HubConnectionStateChangedMessage(_viewerHub.ConnectionState));
        };
        _viewerHub.Reconnecting += async (ex) =>
        {
          await _messenger.Send(new HubConnectionStateChangedMessage(_viewerHub.ConnectionState));
        };
        _viewerHub.Reconnected += async (id) =>
        {
          await _messenger.Send(new HubConnectionStateChangedMessage(_viewerHub.ConnectionState));
        };
        _viewerHub.ConnectThrew += async (ex) =>
        {
          await _messenger.Send(new ToastMessage(ex.Message, MessageSeverity.Error));
        };
        _isConfigured = true;
      }

      await _messenger.Send(new HubConnectionStateChangedMessage(_viewerHub.ConnectionState));
      return result;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error connecting to ViewerHub");
      return false;
    }
  }

  public async Task Disconnect()
  {
    await _viewerHub.DisposeAsync();
  }

  private void ConfigureHttpOptions(HttpConnectionOptions options)
  {
    options.Headers.Add("x-personal-token", _options.PersonalAccessToken);
  }
}
