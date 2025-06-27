namespace ControlR.Libraries.Shared.Dtos.HubDtos.PwshCommandCompletions;

public record PwshCompletionsRequestDto(
  Guid DeviceId,
  Guid TerminalId,
  string LastCompletionInput,
  int LastCursorIndex,
  bool? Forward,
  int Page = 0,
  int PageSize = PwshCompletionsRequestDto.DefaultPageSize)
{
  public const int DefaultPageSize = 50;
}