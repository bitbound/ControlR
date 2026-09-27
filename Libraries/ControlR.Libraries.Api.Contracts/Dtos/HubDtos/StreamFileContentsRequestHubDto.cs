namespace ControlR.Libraries.Api.Contracts.Dtos.HubDtos;

public record StreamFileContentsRequestHubDto(
  Guid StreamId,
  LogKind Kind,
  string FileName,
  string? Username);
