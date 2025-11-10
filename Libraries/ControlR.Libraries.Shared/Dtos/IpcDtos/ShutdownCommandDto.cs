namespace ControlR.Libraries.Shared.Dtos.IpcDtos;

[MessagePackObject(keyAsPropertyName: true)]
public record ShutdownCommandDto(string Reason);