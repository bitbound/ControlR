namespace ControlR.Libraries.Api.Contracts.Dtos.HubDtos;

/// <summary>
/// The consent prompt identifier binds a response or cancellation to one specific prompt. Consent
/// prompts are re-pushed to the same session id after a decline, cancel, or timeout, so without it
/// a delayed click on a stale prompt could approve the newest one.
/// </summary>
[MessagePackObject(keyAsPropertyName: true)]
public record AssistConsentRequestedDto(
  Guid SessionId,
  string ViewerDisplayName,
  string? ViewerEmail,
  Guid ConsentNonce);
