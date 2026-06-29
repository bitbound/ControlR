using ControlR.Libraries.Api.Contracts.Enums;
using MessagePack;

namespace ControlR.Libraries.Api.Contracts.Dtos.IpcDtos;

[MessagePackObject(keyAsPropertyName: true)]
public record ExecuteScriptIpcDto(
  Guid ExecutionId,
  string ScriptContent,
  ShellType ShellType,
  ScriptRunAs RunAs);
