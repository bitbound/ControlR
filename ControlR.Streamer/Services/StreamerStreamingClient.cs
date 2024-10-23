using System.Net.WebSockets;
using Bitbound.SimpleMessenger;
using ControlR.Libraries.Clients.Services;
using ControlR.Libraries.Shared.Dtos.HubDtos;
using ControlR.Libraries.Shared.Services.Buffers;
using ControlR.Streamer.Messages;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace ControlR.Streamer.Services;

public interface IStreamerStreamingClient : IHostedService
{
}

internal sealed class StreamerStreamingClient(
  IMessenger messenger,
  IHostApplicationLifetime appLifetime,
  IToaster toaster,
  IDisplayManager displayManager,
  IMemoryProvider memoryProvider,
  IOptions<StartupOptions> startupOptions,
  ILogger<StreamerStreamingClient> logger)
  : StreamingClient(messenger, memoryProvider, logger), IStreamerStreamingClient
{
  public async Task StartAsync(CancellationToken cancellationToken)
  {
    try
    {
      await Connect(startupOptions.Value.WebSocketUri, appLifetime.ApplicationStopping);
      Messenger.Register<LocalClipboardChangedMessage>(this, HandleLocalClipboardChanged);
      Messenger.Register<DisplaySettingsChangedMessage>(this, HandleDisplaySettingsChanged);
      Messenger.Register<CursorChangedMessage>(this, HandleCursorChangedMessage);

      await SendDisplayData();

      if (startupOptions.Value.NotifyUser)
      {
        var message = startupOptions.Value.ViewerName is { Length: > 0 } viewerName
          ? $"{viewerName} has joined your session"
          : "Remote control session has started";

        await toaster.ShowToast("ControlR", message, BalloonTipIcon.Info);
      }

      StreamScreenToViewer().Forget();
    }
    catch (Exception ex)
    {
      logger.LogError(
        ex,
        "Error while initializing streaming session. " +
        "Streaming cannot start.  Shutting down.");
      appLifetime.StopApplication();
    }
  }

  public async Task StopAsync(CancellationToken cancellationToken)
  {
    await DisposeAsync();
  }

  private async Task HandleCursorChangedMessage(object subscriber, CursorChangedMessage message)
  {
    try
    {
      var dto = new CursorChangedDto(message.Cursor, startupOptions.Value.SessionId);
      var wrapper = DtoWrapper.Create(dto, DtoType.CursorChanged);
      await Send(wrapper, appLifetime.ApplicationStopping);
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Error while handling cursor change.");
    }
  }

  private async Task HandleDisplaySettingsChanged(object subscriber, DisplaySettingsChangedMessage message)
  {
    displayManager.ResetDisplays();
    await SendDisplayData();
  }

  private async Task HandleLocalClipboardChanged(object subscriber, LocalClipboardChangedMessage message)
  {
    try
    {
      var dto = new ClipboardChangeDto(message.Text, startupOptions.Value.SessionId);
      var wrapper = DtoWrapper.Create(dto, DtoType.ClipboardChanged);
      await Send(wrapper, appLifetime.ApplicationStopping);
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Error while handling local clipboard change.");
    }
  }

  private async Task SendDisplayData()
  {
    try
    {
      var displays = displayManager.GetDisplays().ToArray();
      var dto = new DisplayDataDto(
        startupOptions.Value.SessionId,
        displays);

      var wrapper = DtoWrapper.Create(dto, DtoType.DisplayData);
      await Send(wrapper, appLifetime.ApplicationStopping);
    }
    catch (Exception ex)
    {
      logger.LogError(
        ex,
        "Error while sending streamer init data. " +
        "Streaming cannot start.  Shutting down.");
      appLifetime.StopApplication();
    }
  }

  private async Task StreamScreenToViewer()
  {
    await displayManager.StartCapturingChanges();

    while (State == WebSocketState.Open && !appLifetime.ApplicationStopping.IsCancellationRequested)
    {
      try
      {
        await foreach (var region in displayManager.GetChangedRegions())
        {
          var wrapper = DtoWrapper.Create(region, DtoType.ScreenRegion);
          await Send(wrapper, appLifetime.ApplicationStopping);
        }
      }
      catch (OperationCanceledException)
      {
        break;
      }
      catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
      {
        break;
      }
      catch (Exception ex)
      {
        logger.LogError(ex, "Error while sending screen frame.");
        break;
      }
    }

    logger.LogInformation("Streaming session ended.  Shutting down.");
    appLifetime.StopApplication();
  }
}