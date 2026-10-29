using ControlR.DesktopClient.Common;
using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.DesktopClient.Common.ServiceInterfaces.Toaster;
using ControlR.DesktopClient.Common.Services;
using ControlR.Libraries.Api.Contracts.Dtos.RemoteControlDtos;
using ControlR.Libraries.Shared.Extensions;
using ControlR.Libraries.Shared.Constants;
using ControlR.Libraries.WebSocketRelay.Client;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

#if IS_WINDOWS
using ControlR.DesktopClient.Windows;
#elif IS_MACOS
using ControlR.DesktopClient.Mac;
#elif IS_LINUX
using ControlR.DesktopClient.Linux;
#endif

namespace ControlR.Assist.Services;

public sealed record AssistSessionRequest(
  Uri ServerUri,
  Guid SessionId,
  string AccessToken,
  string? ViewerDisplayName);

public interface IAssistSessionHostFactory
{
  IHost CreateSessionHost(AssistSessionRequest request);
}

/// <summary>
/// Builds an isolated generic host per session so the capture and input services can use
/// per-session options and stop cleanly when the session ends, without disturbing the app host.
/// </summary>
public sealed class AssistSessionHostFactory : IAssistSessionHostFactory
{
  private const uint RelayTimeoutSeconds = 30;

  public IHost CreateSessionHost(AssistSessionRequest request)
  {
    var builder = Host.CreateApplicationBuilder();

    builder.AddCommonRemoteControlServices(
      appBuilder => appBuilder.Services
        .AddSingleton<IToaster, LogToaster>()
        .AddSingleton<IRemoteControlDtoSender, RelayOnlyDtoSender>()
        .AddSingleton<ISessionConsentService, AutoApproveConsentService>()
        .AddSingleton<IUserInteractionService, HeadlessUserInteractionService>(),
      options =>
      {
        options.SessionId = request.SessionId;
        options.RequireConsent = false;
        options.NotifyUser = false;
        options.ViewerName = request.ViewerDisplayName;
        options.ViewerConnectionId = null;

        // The technician's browser is a legacy-style viewer for this session: it has no agent
        // behind it, so it sends remote control DTOs on the relay instead of through the hub.
        options.ViewerSupportsRemoteControlDtoHub = false;
        options.WebSocketUri = BuildResponderRelayUri(request);
      },
      options =>
      {
        options.InstanceId = null;
      });

    builder.AddRemoteControlPlatformServices();
    WrapViewerCommandHandler(builder.Services);

    return builder.Build();
  }

  private static Uri BuildResponderRelayUri(AssistSessionRequest request) =>
    RelayUriBuilder.Build(
      request.ServerUri.ToWebsocketUri(),
      AppConstants.WebSocketRelayPath,
      request.SessionId,
      request.AccessToken,
      RelayRole.Responder,
      RelayTimeoutSeconds);

  // The customer keeps authority over the machine: the real command handler is wrapped so view-only
  // drops input, and block-input/privacy-screen requests never reach it.
  private static void WrapViewerCommandHandler(IServiceCollection services)
  {
    var descriptor = services.Last(x => x.ServiceType == typeof(IRemoteControlViewerCommandHandler));
    var implementationType = descriptor.ImplementationType
                             ?? throw new InvalidOperationException("Expected a concrete viewer command handler.");

    services.Remove(descriptor);
    services.AddSingleton<AssistInputGate>();
    services.AddSingleton<IRemoteControlViewerCommandHandler>(provider =>
    {
      var inner = (IRemoteControlViewerCommandHandler)ActivatorUtilities.CreateInstance(
        provider,
        implementationType);

      return new ViewerCommandGate(
        inner,
        provider.GetRequiredService<AssistInputGate>(),
        provider.GetRequiredService<ILogger<ViewerCommandGate>>());
    });
  }

  private sealed class AutoApproveConsentService : ISessionConsentService
  {
    public Task<bool> RequestConsentAsync(string requesterName, CancellationToken cancellationToken) =>
      // Consent is mediated by the assist control plane before the session host starts.
      Task.FromResult(true);
  }

  // No UI in the assist session host; the consent prompt lives in the assist app's main window.
  private sealed class HeadlessUserInteractionService : IUserInteractionService
  {
    public Task<bool> ShowConsentDialogAsync(string requesterName, CancellationToken cancellationToken) =>
      Task.FromResult(true);
  }
  private sealed class LogToaster(ILogger<LogToaster> logger) : IToaster
  {
    public Task ShowToast(string title, string message, ToastIcon toastIcon, TimeSpan? closeAfter = null)
    {
      logger.LogInformation("Remote toast: {Title} - {Message}", title, message);
      return Task.CompletedTask;
    }

    public Task ShowToast(string title, string message, ToastIcon toastIcon, Func<Task> onClick, TimeSpan? closeAfter = null) =>
      ShowToast(title, message, toastIcon, closeAfter);

    public Task ShowToast(string title, string message, ToastIcon toastIcon, Action onClick, TimeSpan? closeAfter = null) =>
      ShowToast(title, message, toastIcon, closeAfter);
  }
  private sealed class RelayOnlyDtoSender : IRemoteControlDtoSender
  {
    public Task<bool> TrySendRemoteControlDto(string viewerConnectionId, RemoteControlDesktopWrapper wrapper) =>
      // Assist sessions never take the hub DTO path; the responder writes everything to the relay.
      Task.FromResult(true);
  }
}
