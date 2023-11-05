using ControlR.Shared.Dtos;
using ControlR.Shared.Models;

namespace ControlR.Shared.Interfaces.HubClients;

public interface IAgentHubClient : IHubClient
{
    Task<VncSessionRequestResult> GetVncSession(SignedPayloadDto sessionRequest);

    Task<WindowsSession[]> GetWindowsSessions(SignedPayloadDto signedDto);
}