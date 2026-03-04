namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi;

[MessagePackObject(keyAsPropertyName: true)]
public record LogFileGroupDto(
  string GroupName,
  List<LogFileEntryDto> LogFiles);
