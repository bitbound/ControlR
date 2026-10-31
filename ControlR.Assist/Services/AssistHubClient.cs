using ControlR.Libraries.Api.Contracts.Dtos.HubDtos;
using ControlR.Libraries.Api.Contracts.Hubs.Clients;

namespace ControlR.Assist.Services;

/// <summary>
/// Receives the assist control plane's pushes and hands them to the session coordinator.
/// </summary>
public sealed class AssistHubClient(AssistSessionCoordinator coordinator) : IAssistHubClient
{
  public Task AssistConsentCancelled(AssistConsentCancelledDto dto) => coordinator.HandleConsentCancelled(dto);

  public Task AssistConsentRequested(AssistConsentRequestedDto dto) => coordinator.HandleConsentRequested(dto);

  // Only technicians and the browser viewer ever receive this one.
  public Task AssistConsentResponded(AssistConsentRespondedDto dto) => Task.CompletedTask;

  public Task AssistConsentTimedOut(AssistConsentTimedOutDto dto) => coordinator.HandleConsentTimedOut(dto);

  public Task AssistSessionEnded(AssistSessionEndedDto dto) => coordinator.HandleSessionEnded(dto);

  public Task AssistSessionStarted(AssistSessionStartedDto dto) => coordinator.HandleSessionStarted(dto);
}