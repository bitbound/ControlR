using ControlR.Libraries.Api.Contracts.Dtos.Ui;
using ControlR.Libraries.Api.Contracts.Hubs.Clients;

namespace ControlR.Web.Client.Services;

public class ViewerHubClient(IMessenger messenger)
  : IViewerHubClient
{
  private readonly IMessenger _messenger = messenger;

  public async Task InvokeToast(ToastInfo toastInfo)
  {
    var toastMessage = new MudToastMessage(
      toastInfo.Message,
      toastInfo.MessageSeverity.ToMudSeverity());

    await _messenger.Send(toastMessage);
  }

  public async Task<bool> ReceiveChatResponse(ChatResponseHubDto dto)
  {
    var exceptions = await _messenger.Send(new DtoReceivedMessage<ChatResponseHubDto>(dto));
    return exceptions.Count == 0;
  }

  public async Task ReceiveDeviceUpdate(InternalDtos.DeviceResponseDto deviceDto)
  {
    await _messenger.Send(new DtoReceivedMessage<InternalDtos.DeviceResponseDto>(deviceDto));
  }

  public async Task ReceiveDto(DtoWrapper dto)
  {
    await _messenger.Send(new DtoReceivedMessage<DtoWrapper>(dto));
  }

  public async Task ReceiveServerStats(InternalDtos.ServerStatsDto serverStats)
  {
    var message = new DtoReceivedMessage<InternalDtos.ServerStatsDto>(serverStats);
    await _messenger.Send(message);
  }

  public async Task ReceiveTerminalOutput(TerminalOutputDto output)
  {
    await _messenger.Send(new DtoReceivedMessage<TerminalOutputDto>(output));
  }
}