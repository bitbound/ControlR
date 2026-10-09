namespace ControlR.Libraries.Api.Contracts.Dtos.AgentApi;

/// <summary>
/// The body an agent signs to prove a REST request came from it. It names the request it authorizes,
/// so a signature captured from one call cannot be replayed against a different verb or path.
/// A request that carries a body must extend this shape with a hash of that body.
/// </summary>
[MessagePackObject(keyAsPropertyName: true)]
public record AgentRequestAttestationDto(
  Guid DeviceId,
  string Method,
  string PathAndQuery);
