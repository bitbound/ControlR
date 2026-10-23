namespace ControlR.Libraries.Api.Contracts.Dtos.HubDtos;

[MessagePackObject(keyAsPropertyName: true)]
public record StreamFileContentsRequestHubDto(
  Guid StreamId,
  LogKind Kind,
  string FileName,
  string? Username);
