namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

public record DeletePathRequestDto(
  Guid DeviceId,
  string FilePath);
