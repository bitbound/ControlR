namespace ControlR.Libraries.Api.Contracts.Dtos.HubDtos;

public record StreamFileContentsRequestHubDto(
  Guid StreamId,
  string FilePath);
