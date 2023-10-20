using ControlR.Shared.Dtos;
using ControlR.Shared.Models;

namespace ControlR.Shared.Interfaces.HubClients;
public interface IAgentHubClient : IHubClient
{
    Task<bool> GetStreamingSession(SignedPayloadDto sessionRequest);
    Task<WindowsSession[]> GetWindowsSessions(SignedPayloadDto signedDto);
}
