using ControlR.Libraries.Api.Contracts.Dtos.HubDtos;

namespace ControlR.Libraries.Api.Contracts.Hubs.Clients;

public interface IAssistHubClient
{
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
