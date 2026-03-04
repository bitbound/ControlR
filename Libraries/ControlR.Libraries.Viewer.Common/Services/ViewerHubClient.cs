using ControlR.Libraries.Api.Contracts.Hubs.Clients;

namespace ControlR.Libraries.Viewer.Common.Services;

public class ViewerHubClient(IMessenger messenger, ILogger<ViewerHubClient> logger)
  : IViewerHubClient
{
  private readonly ILogger<ViewerHubClient> _logger = logger;
  private readonly IMessenger _messenger = messenger;

  public async Task InvokeToast(ToastInfo toastInfo)
  {
    _logger.LogInformation("Toast: {Message}", toastInfo.Message);
    var toastMessage = new ToastMessage(
      toastInfo.Message,
      MessageSeverity.Information);

    await _messenger.Send(toastMessage);
  }

  public async Task<bool> ReceiveChatResponse(ChatResponseHubDto dto)
  {
    var exceptions = await _messenger.Send(new DtoReceivedMessage<ChatResponseHubDto>(dto));
    return exceptions.Count == 0;
  }

  public async Task ReceiveDeviceUpdate(DeviceResponseDto deviceDto)
  {
    await _messenger.Send(new DtoReceivedMessage<DeviceResponseDto>(deviceDto));
  }

  public async Task ReceiveServerStats(ServerStatsDto serverStats)
  {
    var message = new DtoReceivedMessage<ServerStatsDto>(serverStats);
    await _messenger.Send(message);
  }

  public async Task ReceiveTerminalOutput(TerminalOutputDto output)
  {
    await _messenger.Send(new DtoReceivedMessage<TerminalOutputDto>(output));
  }
}
