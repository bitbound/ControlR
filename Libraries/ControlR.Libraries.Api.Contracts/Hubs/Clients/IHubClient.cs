using ControlR.Libraries.Api.Contracts.Dtos.RemoteControlDtos;

namespace ControlR.Libraries.Api.Contracts.Hubs.Clients;

public interface IHubClient
{
  Task ReceiveDto(DtoWrapper dtoWrapper);
}