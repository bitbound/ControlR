using ControlR.Libraries.Shared.Models;

namespace ControlR.Libraries.Shared.Dtos.HubDtos;

[MessagePackObject(keyAsPropertyName: true)]
public record InvokeCtrlAltDelRequestDto(
  int TargetDesktopProcessId, 
  string InvokerUserName, 
  DesktopSessionType DesktopSessionType);
