using ControlR.Libraries.Api.Contracts.Hubs.Clients;

namespace ControlR.Web.Client.Services;

public class AssistHubClient(IMessenger messenger) : IAssistHubClient
{
  private readonly IMessenger _messenger = messenger;

  public Task AssistConsentRequested(AssistConsentRequestedDto dto) =>
    // Only the assist app side receives this. The web client never renders it.
    Task.CompletedTask;

  public Task AssistConsentResponded(AssistConsentRespondedDto dto) =>
    _messenger.Send(new DtoReceivedMessage<AssistConsentRespondedDto>(dto));

  public Task AssistSessionEnded(AssistSessionEndedDto dto) =>
    _messenger.Send(new DtoReceivedMessage<AssistSessionEndedDto>(dto));

  public Task AssistSessionStarted(AssistSessionStartedDto dto) =>
    // Only the assist app side receives this. The technician learns the session started
    // through AssistConsentResponded.
    Task.CompletedTask;
}
