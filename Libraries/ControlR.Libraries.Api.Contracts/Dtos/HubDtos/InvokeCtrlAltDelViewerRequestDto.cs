namespace ControlR.Libraries.Api.Contracts.Dtos.HubDtos;

using ControlR.Libraries.Api.Contracts.Dtos.Devices;

[MessagePackObject(keyAsPropertyName: true)]
public record InvokeCtrlAltDelViewerRequestDto(
  Guid DeviceId,
  int TargetDesktopProcessId,
  DesktopSessionType DesktopSessionType);
