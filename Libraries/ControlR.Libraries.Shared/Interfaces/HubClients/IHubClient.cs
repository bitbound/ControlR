namespace ControlR.Libraries.Shared.Interfaces.HubClients;

public interface IHubClient
{
  Task ReceiveDto(DtoWrapper dtoWrapper);
}