using ControlR.Shared.Dtos;
using ControlR.Shared.Models;

namespace ControlR.Shared.Interfaces.HubClients;

public interface IAgentHubClient : IHubClient
{
    Task<bool> GetVncSession(SignedPayloadDto sessionRequest);

    Task<WindowsSession[]> GetWindowsSessions(SignedPayloadDto signedDto);
}