using ControlR.DesktopClient.Common;
using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.DesktopClient.Common.ServiceInterfaces.Toaster;
using ControlR.DesktopClient.Common.Services;
using ControlR.Libraries.Api.Contracts.Dtos.RemoteControlDtos;
using ControlR.Libraries.Avalonia.Services;
using ControlR.Libraries.Shared.Extensions;
using ControlR.Libraries.Shared.Constants;
using ControlR.Libraries.Shared.Services.Locks;
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
  string? ViewerDisplayName,
  RemoteControlCommandPolicyDto? CommandPolicy);

public interface IAssistSessionHostFactory
{
  IHost CreateSessionHost(AssistSessionRequest request);
}

/// <summary>
/// Builds an isolated generic host per session so the capture and input services can use
/// per-session options and stop cleanly when the session ends, without disturbing the app host.
/// </summary>
public sealed class AssistSessionHostFactory(
  ILoggerFactory loggerFactory,
  IUiDispatcher dispatcher,
  IAsyncLock asyncLock) : IAssistSessionHostFactory
{
  private const uint RelayTimeoutSeconds = 30;

  private readonly IAsyncLock _asyncLock = asyncLock;
  private readonly IUiDispatcher _dispatcher = dispatcher;
  private readonly ILoggerFactory _loggerFactory = loggerFactory;

  public IHost CreateSessionHost(AssistSessionRequest request)
  {
    var builder = Host.CreateApplicationBuilder();

    builder.AddCommonRemoteControlServices(
      new RemoteControlHostDependencies
      {
        Toaster = new LogToaster(_loggerFactory.CreateLogger<LogToaster>()),
        UserInteraction = new HeadlessUserInteractionService(),
        Consent = new AutoApproveConsentService(),
        Dispatcher = _dispatcher,
        AsyncLock = _asyncLock
      },
      options =>
      {
        options.SessionId = request.SessionId;
        options.CommandPolicy = request.CommandPolicy;
        options.RequireConsent = false;
        options.NotifyUser = false;
        options.ViewerName = request.ViewerDisplayName;
        options.ViewerConnectionId = null;
        options.WebSocketUri = BuildResponderRelayUri(request);
      },
      options =>
      {
        options.InstanceId = null;
      });

    builder.AddRemoteControlPlatformServices();
    WrapViewerCommandHandler(builder.Services);
    builder.ConfigureRemoteControlHostValidation();

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

  // The customer keeps authority over the machine. The real command handler is wrapped so view-only
  // drops input, and block-input/privacy-screen requests never reach it. The captured factory is the
  // policy-gated handler from AddCommonRemoteControlServices, so the final chain is
  // ViewerCommandGate -> command policy gate -> real handler.
  private static void WrapViewerCommandHandler(IServiceCollection services)
  {
    var descriptor = services.Last(x => x.ServiceType == typeof(IRemoteControlViewerCommandHandler));
    var innerFactory = descriptor.ImplementationFactory
                       ?? throw new InvalidOperationException("Expected a factory-registered viewer command handler.");

    services.Remove(descriptor);
    services.AddSingleton<AssistInputGate>();
    services.AddSingleton<IRemoteControlViewerCommandHandler>(provider =>
    {
      var inner = (IRemoteControlViewerCommandHandler)innerFactory(provider);

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

  // No UI in the assist session host. The consent prompt lives in the assist app's main window.
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
}
