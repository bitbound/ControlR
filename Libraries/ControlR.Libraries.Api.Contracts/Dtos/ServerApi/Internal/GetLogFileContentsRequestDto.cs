namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

public record GetLogFileContentsRequestDto(
  LogKind Kind,
  string FileName,
  string? Username);
