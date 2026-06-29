using MessagePack;

namespace ControlR.Libraries.Api.Contracts.Dtos.IpcDtos;

[MessagePackObject(keyAsPropertyName: true)]
public record ScriptOutputIpcDto(
  Guid ExecutionId,
  string StdOut,
  string StdErr,
  bool IsFinished,
  int? ExitCode);
