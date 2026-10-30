using ControlR.Libraries.Api.Contracts.Dtos.RemoteControlDtos;

namespace ControlR.Libraries.Api.Contracts.Dtos.HubDtos;

[MessagePackObject(keyAsPropertyName: true)]
public record AssistSessionStartedDto(
  Guid SessionId,
  string AccessToken,
  string ViewerDisplayName,
  string? ViewerEmail,
  RemoteControlCommandPolicyDto? CommandPolicy);
