namespace ControlR.Libraries.Shared.Hubs;

public interface IBrowserHubBase
{
  Task SendDtoToAgent(Guid deviceId, DtoWrapper wrapper);
  Task SendDtoToUserGroups(DtoWrapper wrapper);
}