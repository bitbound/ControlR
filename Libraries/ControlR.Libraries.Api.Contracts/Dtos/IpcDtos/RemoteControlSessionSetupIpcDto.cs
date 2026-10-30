namespace ControlR.Libraries.Api.Contracts.Dtos.IpcDtos;

/// <summary>
/// Returned by the desktop client when it starts a remote control session host.
/// </summary>
/// <param name="RelayCommandsGated">
/// True when the session host opened its relay command lane with a valid sealed command policy.
/// Only then may the viewer send remote control DTOs on the relay.
/// </param>
[MessagePackObject(keyAsPropertyName: true)]
public record RemoteControlSessionSetupIpcDto(bool RelayCommandsGated);
