namespace ControlR.Libraries.Api.Contracts.Dtos.HubDtos;

/// <summary>
/// Returned to the viewer when a remote control session starts.
/// </summary>
/// <param name="UseRelayCommands">
/// True when the responder accepts remote control DTOs on the WebSocket relay under a sealed
/// command policy. False keeps the viewer on the hub command path, where the server authorizes
/// every command.
/// </param>
[MessagePackObject(keyAsPropertyName: true)]
public record RequestRemoteControlSessionResponseDto(bool UseRelayCommands);
