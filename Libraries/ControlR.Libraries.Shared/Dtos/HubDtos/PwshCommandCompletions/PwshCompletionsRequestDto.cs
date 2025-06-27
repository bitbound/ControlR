namespace ControlR.Libraries.Shared.Dtos.HubDtos.PwshCommandCompletions;

public record PwshCompletionsRequestDto(
  Guid DeviceId,
  Guid TerminalId,
  string LastCompletionInput,
  int LastCursorIndex,
  bool Forward);