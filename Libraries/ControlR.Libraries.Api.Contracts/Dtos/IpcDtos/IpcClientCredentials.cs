namespace ControlR.Libraries.Api.Contracts.Dtos.IpcDtos;

[MessagePackObject(keyAsPropertyName: true)]
public record IpcClientCredentials(int ProcessId, string ExecutablePath);
