using ControlR.Libraries.Shared.Dtos;

namespace ControlR.Libraries.Shared.Interfaces.HubClients;

public interface IHubClient
{
    Task ReceiveDto(SignedPayloadDto dto);
}