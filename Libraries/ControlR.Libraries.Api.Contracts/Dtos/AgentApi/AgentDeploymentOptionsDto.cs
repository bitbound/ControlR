namespace ControlR.Libraries.Api.Contracts.Dtos.AgentApi;

/// <summary>
/// Deployment values the server wants this device's install to use. A null member means the server
/// has no opinion, so the agent keeps what it has.
/// </summary>
public record AgentDeploymentOptionsDto(
  string? InstanceId);
