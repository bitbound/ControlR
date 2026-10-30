namespace ControlR.Libraries.Api.Contracts.Dtos.HubDtos;

/// <summary>
/// Returned by the agent when a remote control session starts.
/// </summary>
/// <param name="ResponderCommandsOnRelay">
/// True when the responder opened its relay command lane under a valid sealed command policy, so
/// the viewer may send remote control DTOs on the relay. A responder that predates command
/// policies reports false (or no value at all) and must keep receiving commands over the hub.
/// </param>
[MessagePackObject(keyAsPropertyName: true)]
public record CreateRemoteControlSessionResponseDto(bool ResponderCommandsOnRelay);
