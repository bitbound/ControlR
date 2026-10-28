namespace ControlR.Libraries.Api.Contracts.Dtos.HubDtos.PwshCommandCompletions;

[MessagePackObject(keyAsPropertyName: true)]
public record PwshCompletionsRequestDto(
  Guid DeviceId,
  Guid TerminalId,
  string LastCompletionInput,
  int LastCursorIndex,
  string ViewerConnectionId,
  bool? Forward,
  int Page = 0,
  int PageSize = PwshCompletionsRequestDto.DefaultPageSize)
{
  public const int DefaultPageSize = 50;
}