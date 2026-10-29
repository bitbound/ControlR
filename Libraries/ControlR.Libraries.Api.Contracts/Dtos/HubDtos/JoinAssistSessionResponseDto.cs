namespace ControlR.Libraries.Api.Contracts.Dtos.HubDtos;

/// <summary>
/// The consent nonce identifies the prompt this join started, so the viewer can cancel exactly that
/// prompt even if the session has since cycled through other prompts.
/// </summary>
[MessagePackObject(keyAsPropertyName: true)]
public record JoinAssistSessionResponseDto(
  Guid SessionId,
  Guid ConsentNonce);
