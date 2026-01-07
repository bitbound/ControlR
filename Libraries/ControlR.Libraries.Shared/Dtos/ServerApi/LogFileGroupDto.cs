namespace ControlR.Libraries.Shared.Dtos.ServerApi;

[MessagePackObject(keyAsPropertyName: true)]
public record LogFileGroupDto(
  string GroupName,
  List<LogFileEntryDto> LogFiles);
