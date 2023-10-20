using ControlR.Shared.Dtos;

namespace ControlR.Shared.Interfaces.HubClients;

public interface IHubClient
{
    Task ReceiveDto(SignedPayloadDto dto);
}