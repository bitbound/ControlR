using ControlR.Libraries.Api.Contracts.Dtos.Devices;

namespace ControlR.Libraries.Api.Contracts.Dtos.HubDtos;

[MessagePackObject(keyAsPropertyName: true)]
public record InvokeCtrlAltDelRequestDto(
  int TargetDesktopProcessId, 
  string InvokerUserName, 
  DesktopSessionType DesktopSessionType);
