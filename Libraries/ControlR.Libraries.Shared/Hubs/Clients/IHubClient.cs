namespace ControlR.Libraries.Shared.Hubs.Clients;

public interface IHubClient
{
  Task ReceiveDto(DtoWrapper dtoWrapper);
}