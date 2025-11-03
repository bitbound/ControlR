namespace ControlR.Libraries.Shared.Hubs;

public interface IBrowserHubBase
{
  Task RefreshDeviceInfo(Guid deviceId);
  Task SendDtoToAgent(Guid deviceId, DtoWrapper wrapper);
  Task SendDtoToUserGroups(DtoWrapper wrapper);
}