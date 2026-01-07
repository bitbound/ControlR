namespace ControlR.Libraries.Shared.Dtos.ServerApi;

[MessagePackObject(keyAsPropertyName: true)]
public record GetLogFilesResponseDto(List<LogFileGroupDto> LogFileGroups);
