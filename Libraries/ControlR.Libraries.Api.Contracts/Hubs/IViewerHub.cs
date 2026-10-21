using System.Threading.Channels;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.HubDtos;
using ControlR.Libraries.Api.Contracts.Dtos.HubDtos.PwshCommandCompletions;
using ControlR.Libraries.Api.Contracts.Dtos.RemoteControlDtos;

namespace ControlR.Libraries.Api.Contracts.Hubs;

public partial interface IViewerHub
{
  Task<HubResult> AddViewerActivity2(AddViewerActivityRequestDto request);

  Task<HubResult> CloseChatSession2(CloseChatSessionRequestDto request);

  Task<HubResult> CloseTerminalSession2(CloseTerminalSessionRequestDto request);

  Task<HubResult> CreateTerminalSession2(CreateTerminalSessionRequestDto request);
  Task<HubResult> DisposeDeviceAccessActivity();

  Task<HubResult<ActiveDesktopSessionsResponseDto>> GetActiveDesktopSessions2(GetActiveDesktopSessionsRequestDto request);

  Task<HubResult<DeviceAccessPermissionsDto>> GetDeviceAccessPermissions2(GetDeviceAccessPermissionsRequestDto request);
  Task<HubResult<PwshCompletionsResponseDto>> GetPwshCompletions(PwshCompletionsRequestDto request);

  Task<HubResult> InvokeCtrlAltDel2(InvokeCtrlAltDelViewerRequestDto request);
  Task<HubResult> InvokeRemoteControlDtoOnAgent(SendRemoteControlDtoToAgentRequestDto request);

  Task<HubResult> RefreshDeviceInfo2(RefreshDeviceInfoRequestDto request);

  Task<HubResult> RequestRemoteControlPermission2(RequestRemoteControlPermissionRequestDto request);

  Task<HubResult> RequestRemoteControlSession3(RemoteControlSessionRequestDto sessionRequestDto);

  Task<HubResult> RequestVncSession2(VncSessionRequestDto sessionRequestDto);

  Task<HubResult> SendAgentUpdateTrigger2(SendAgentUpdateTriggerRequestDto request);

  Task<HubResult> SendChatMessage2(ChatMessageHubDto dto);

  Task<HubResult> SendPowerStateChange2(SendPowerStateChangeRequestDto request);

  Task SendRemoteControlDtoToAgent(SendRemoteControlDtoToAgentRequestDto request);

  Task<HubResult> SendTerminalInput2(SendTerminalInputRequestDto request);

  Task<HubResult<string>> SendWakeDevice2(SendWakeDeviceRequestDto request);

  Task<HubResult> StartDeviceAccessActivity2(StartDeviceAccessActivityRequestDto request);

  Task<HubResult> SubscribeToDeviceHeartbeats2(SubscribeToDeviceHeartbeatsRequestDto request);

  Task<HubResult> TestVncConnection2(TestVncConnectionRequestDto request);

  Task<HubResult> UninstallAgent2(UninstallAgentRequestDto request);

  Task<HubResult> UnsubscribeFromDeviceHeartbeats2(UnsubscribeFromDeviceHeartbeatsRequestDto request);

  Task<HubResult> UploadFile(FileUploadMetadata metadata, ChannelReader<byte[]> fileStream);
}