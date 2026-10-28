using ControlR.Libraries.Api.Contracts.Dtos.HubDtos;

namespace ControlR.Libraries.Api.Contracts.Hubs.Clients;

public interface IAssistHubClient
{
  Task AssistConsentRequested(AssistConsentRequestedDto dto);
  Task AssistConsentResponded(AssistConsentRespondedDto dto);
  Task AssistSessionEnded(AssistSessionEndedDto dto);
  Task AssistSessionStarted(AssistSessionStartedDto dto);
}
