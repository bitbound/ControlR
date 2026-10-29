using ControlR.Libraries.Api.Contracts.Dtos.HubDtos;

namespace ControlR.Libraries.Api.Contracts.Hubs.Clients;

public interface IAssistHubClient
{
  /// <summary>
  /// Sent to the assist app when the technician cancels a pending consent request, or drops the
  /// connection while consent is pending. The session returns to waiting so the same code stays
  /// usable. Viewers never receive this, so it is a no-op default for browser clients.
  /// </summary>
  Task AssistConsentCancelled(AssistConsentCancelledDto dto) => Task.CompletedTask;
  Task AssistConsentRequested(AssistConsentRequestedDto dto);
  Task AssistConsentResponded(AssistConsentRespondedDto dto);

  /// <summary>
  /// Sent to the assist app when an unanswered consent request expires. Viewers learn about the
  /// same expiry through <see cref="AssistConsentResponded"/> instead, so this is a no-op default
  /// for browser clients.
  /// </summary>
  Task AssistConsentTimedOut(AssistConsentTimedOutDto dto) => Task.CompletedTask;
  Task AssistSessionEnded(AssistSessionEndedDto dto);
  Task AssistSessionStarted(AssistSessionStartedDto dto);
}
