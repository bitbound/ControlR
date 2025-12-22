using ControlR.Libraries.Shared.Dtos.RemoteControlDtos;

namespace ControlR.Libraries.Shared.Hubs.Clients;

public interface IHubClient
{
  Task ReceiveDto(DtoWrapper dtoWrapper);
}