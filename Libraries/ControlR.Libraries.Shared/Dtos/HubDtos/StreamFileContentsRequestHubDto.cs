namespace ControlR.Libraries.Shared.Dtos.HubDtos;

public record StreamFileContentsRequestHubDto(
  Guid StreamId,
  string FilePath);
