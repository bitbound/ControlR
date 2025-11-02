using ControlR.Libraries.Shared.Dtos.StreamerDtos;
using ControlR.Libraries.Shared.Hubs.Clients;

namespace ControlR.Web.Client.Services.DeviceAccess;

public class DeviceAccessHubClient(
  IMessenger messenger,
  IDeviceStore deviceStore) 
  : BrowserHubClientBase(messenger, deviceStore), IDeviceAccessHubClient
{

  public async Task<bool> ReceiveChatResponse(ChatResponseHubDto dto)
  {
    var exceptions = await Messenger.Send(new DtoReceivedMessage<ChatResponseHubDto>(dto));
    return exceptions.Count == 0;
  }

  public async Task ReceiveTerminalOutput(TerminalOutputDto output)
  {
    await Messenger.Send(new DtoReceivedMessage<TerminalOutputDto>(output));
  }
}
