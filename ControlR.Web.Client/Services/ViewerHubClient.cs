using ControlR.Libraries.Shared.Dtos.RemoteControlDtos;
using ControlR.Libraries.Shared.Hubs.Clients;
using ControlR.Web.Client.Extensions;

namespace ControlR.Web.Client.Services;

public class ViewerHubClient(IMessenger messenger)
  : IViewerHubClient
{
  private readonly IMessenger _messenger = messenger;

  public async Task InvokeToast(ToastInfo toastInfo)
  {
    var toastMessage = new ToastMessage(
      toastInfo.Message,
      toastInfo.MessageSeverity.ToMudSeverity());

    await _messenger.Send(toastMessage);
  }

  public async Task<bool> ReceiveChatResponse(ChatResponseHubDto dto)
  {
    var exceptions = await _messenger.Send(new DtoReceivedMessage<ChatResponseHubDto>(dto));
    return exceptions.Count == 0;
  }

  public async Task ReceiveDeviceUpdate(DeviceDto deviceDto)
  {
    await _messenger.Send(new DtoReceivedMessage<DeviceDto>(deviceDto));
  }

  public async Task ReceiveDto(DtoWrapper dto)
  {
    await _messenger.Send(new DtoReceivedMessage<DtoWrapper>(dto));
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