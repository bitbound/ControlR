namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.DeploymentOptions;

public sealed record DeploymentOptionsDto(
  bool AppendInstanceId,
  string? InstanceId);