using System.Net;
using System.Threading.Channels;
using ControlR.Libraries.Api.Contracts.Dtos.HubDtos;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ControlR.Assist.Services;

public enum AssistSessionState
{
  Disconnected,
  Connecting,
  Ready,
  PendingConsent,
  Active
}

/// <summary>
/// Drives the assist lifecycle: hub connection, one-time code, consent, and the per-session
/// capture host. All state mutations run serially on a single background work queue, and hub
/// event handlers only post to that queue. The SignalR receive loop is never blocked waiting on
/// a lock that a queue item holds while awaiting a hub response, which is the cycle that used to
/// wedge the app when a session was ended.
/// </summary>
public sealed class AssistSessionController : IAsyncDisposable
{
  private static readonly TimeSpan _invokeTimeout = TimeSpan.FromSeconds(15);
  private static readonly TimeSpan _queueDrainTimeout = TimeSpan.FromSeconds(5);

  private readonly IAssistSignalRClient _hubClient;
  private readonly ILogger<AssistSessionController> _logger;
  private readonly Task _queueLoop;
  private readonly IAssistSessionHostFactory _sessionHostFactory;
  private readonly IAssistSettingsService _settingsService;
  private readonly Channel<Func<Task>> _workQueue = Channel.CreateUnbounded<Func<Task>>();

  private Guid? _activeSessionId;
  private Uri? _connectedServerUri;
  private IHost? _sessionHost;

  public AssistSessionController(
    IAssistSignalRClient hubClient,
    IAssistSessionHostFactory sessionHostFactory,
    IAssistSettingsService settingsService,
    ILogger<AssistSessionController> logger)
  {
    _hubClient = hubClient;
    _sessionHostFactory = sessionHostFactory;
    _settingsService = settingsService;
    _logger = logger;
    _queueLoop = Task.Run(ProcessQueueAsync);
  }

  public event Action? StateChanged;

  public string? Code { get; private set; }
  public bool IsViewOnly => _sessionHost?.Services.GetService<AssistInputGate>()?.IsViewOnly ?? false;
  public string? PendingConsentEmail { get; private set; }
  public Guid? PendingConsentNonce { get; private set; }
  public string? PendingConsentViewerName { get; private set; }
  public Guid? PendingSessionId { get; private set; }
  public AssistSessionState State { get; private set; } = AssistSessionState.Disconnected;
  public string? StatusMessage { get; private set; }

  /// <summary>
  /// Normalizes user-entered server input to scheme://host[:port]. Bare hostnames get an https://
  /// prefix. The host must be a plausible hostname (dotted), localhost, or an IP address, and any
  /// path, query, or fragment is dropped.
  /// </summary>
  public static bool TryNormalizeServerUri(string? input, out Uri serverUri)
  {
    serverUri = null!;

    var candidate = (input ?? string.Empty).Trim();
    if (candidate.Length == 0 || candidate.Any(char.IsWhiteSpace))
    {
      return false;
    }

    if (!candidate.Contains("://"))
    {
      candidate = $"https://{candidate}";
    }

    if (!Uri.TryCreate(candidate, UriKind.Absolute, out var parsed) ||
        (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps))
    {
      return false;
    }

    string host;
    try
    {
      // Throws for inputs like "http://" that parse but carry an empty host.
      host = parsed.Host;
    }
    catch (InvalidOperationException)
    {
      return false;
    }

    if (!IsPlausibleHost(host))
    {
      return false;
    }

    serverUri = new UriBuilder(parsed.Scheme, host, parsed.Port).Uri;
    return true;
  }

  public Task<bool> ConnectAsync(string serverUrl, CancellationToken cancellationToken) =>
    EnqueueAsync(() => ConnectCoreAsync(serverUrl, cancellationToken));

  public Task DisconnectAsync() => EnqueueAsync(DisconnectCoreAsync);

  public async ValueTask DisposeAsync()
  {
    _workQueue.Writer.TryComplete();

    try
    {
      await _queueLoop.WaitAsync(_queueDrainTimeout).ConfigureAwait(false);
    }
    catch (Exception ex)
    {
      _logger.LogWarning(ex, "Assist work queue did not drain before shutdown.");
    }

    await StopSessionHostAsync().ConfigureAwait(false);
    await _hubClient.DisposeAsync().ConfigureAwait(false);
  }

  public Task EndSessionAsync() => EnqueueAsync(EndSessionCoreAsync);

  public Task GenerateNewCodeAsync() => EnqueueAsync(GenerateNewCodeCoreAsync);

  public Task HandleConnectionLost()
  {
    Post(HandleConnectionLostCoreAsync);
    return Task.CompletedTask;
  }

  public Task HandleConsentCancelled(AssistConsentCancelledDto dto)
  {
    Post(() => HandleConsentCancelledCoreAsync(dto));
    return Task.CompletedTask;
  }

  public Task HandleConsentRequested(AssistConsentRequestedDto dto)
  {
    Post(() => HandleConsentRequestedCoreAsync(dto));
    return Task.CompletedTask;
  }

  public Task HandleConsentTimedOut(AssistConsentTimedOutDto dto)
  {
    Post(() => HandleConsentTimedOutCoreAsync(dto));
    return Task.CompletedTask;
  }

  public Task HandleReconnected()
  {
    Post(HandleReconnectedCoreAsync);
    return Task.CompletedTask;
  }

  public Task HandleSessionEnded(AssistSessionEndedDto dto)
  {
    Post(() => HandleSessionEndedCoreAsync(dto));
    return Task.CompletedTask;
  }

  public Task HandleSessionStarted(AssistSessionStartedDto dto)
  {
    Post(() => HandleSessionStartedCoreAsync(dto));
    return Task.CompletedTask;
  }

  public Task RespondToConsentAsync(bool approved) => EnqueueAsync(() => RespondToConsentCoreAsync(approved));

  public void SetViewOnly(bool enabled)
  {
    if (_sessionHost?.Services.GetService<AssistInputGate>() is not { } gate)
    {
      return;
    }

    gate.IsViewOnly = enabled;
    _logger.LogInformation("Customer {State} view-only mode.", enabled ? "enabled" : "disabled");
    RaiseStateChanged();
  }

  private static bool IsPlausibleHost(string host)
  {
    if (host.Length == 0)
    {
      return false;
    }

    if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
        IPAddress.TryParse(host, out _))
    {
      return true;
    }

    // Reject bare labels such as "sdf" that Uri parses happily but that can never resolve.
    var dotIndex = host.IndexOf('.');
    return dotIndex > 0 && dotIndex < host.Length - 1;
  }

  private async Task<bool> ConnectCoreAsync(string serverUrl, CancellationToken cancellationToken)
  {
    if (!TryNormalizeServerUri(serverUrl, out var serverUri))
    {
      StatusMessage = Localization.StatusInvalidUrl;
      RaiseStateChanged();
      return false;
    }

    SetState(AssistSessionState.Connecting, Localization.StatusConnecting);

    if (!await _hubClient.ConnectAsync(serverUri, cancellationToken).ConfigureAwait(false))
    {
      StatusMessage = Localization.StatusUnreachable;
      SetState(AssistSessionState.Disconnected);
      RaiseStateChanged();
      return false;
    }

    var savedSettings = _settingsService.GetSettings();
    AssistSettingsService.RememberUrl(savedSettings, serverUri.ToString());
    await _settingsService.SaveAsync(savedSettings, cancellationToken).ConfigureAwait(false);

    _connectedServerUri = serverUri;
    var created = await CreateSessionAsync(cancellationToken).ConfigureAwait(false);
    if (!created)
    {
      await _hubClient.DisconnectAsync().ConfigureAwait(false);
      _connectedServerUri = null;
      SetState(AssistSessionState.Disconnected);
      RaiseStateChanged();
      return false;
    }

    return true;
  }

  private async Task<bool> CreateSessionAsync(CancellationToken cancellationToken)
  {
    var created = await CreateSessionCoreAsync(cancellationToken).ConfigureAwait(false);
    RaiseStateChanged();
    return created;
  }

  private async Task<bool> CreateSessionCoreAsync(CancellationToken cancellationToken)
  {
    var result = await InvokeHubAsync(ct => _hubClient.CreateAssistSession(ct));
    if (!result.IsSuccess || result.Value is null)
    {
      Code = null;
      _activeSessionId = null;
      StatusMessage = result.Reason ?? Localization.StatusSessionCreateFailed;
      SetState(AssistSessionState.Disconnected);
      return false;
    }

    Code = result.Value.Code;
    _activeSessionId = result.Value.SessionId;
    StatusMessage = null;
    SetState(AssistSessionState.Ready);
    return true;
  }

  private async Task DisconnectCoreAsync()
  {
    await EndCurrentSessionAsync().ConfigureAwait(false);
    await StopSessionHostAsync().ConfigureAwait(false);
    await _hubClient.DisconnectAsync().ConfigureAwait(false);
    _connectedServerUri = null;
    Code = null;
    SetConsentPending(null, null, null, null);
    StatusMessage = null;
    SetState(AssistSessionState.Disconnected);
    RaiseStateChanged();
  }

  private async Task EndCurrentSessionAsync()
  {
    if (_activeSessionId is not Guid sessionId)
    {
      return;
    }

    _ = await InvokeHubAsync(ct => _hubClient.EndAssistSession(sessionId, ct));
    _activeSessionId = null;
  }

  private async Task EndSessionCoreAsync()
  {
    if (State != AssistSessionState.Active)
    {
      return;
    }

    // End on the server while the relay is still up so the technician reliably receives the
    // "ended by remote user" notification before the relay drop arrives.
    await EndCurrentSessionAsync().ConfigureAwait(false);
    await StopSessionHostAsync().ConfigureAwait(false);
    SetConsentPending(null, null, null, null);
    StatusMessage = Localization.StatusSessionEnded;
    await CreateSessionCoreAsync(CancellationToken.None).ConfigureAwait(false);
    RaiseStateChanged();
  }

  private Task EnqueueAsync(Func<Task> work) => EnqueueAsync<object?>(async () =>
  {
    await work().ConfigureAwait(false);
    return null;
  });

  private Task<T> EnqueueAsync<T>(Func<Task<T>> work)
  {
    var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

    if (!_workQueue.Writer.TryWrite(async () =>
    {
      try
      {
        tcs.SetResult(await work().ConfigureAwait(false));
      }
      catch (Exception ex)
      {
        tcs.SetException(ex);
        throw;
      }
    }))
    {
      tcs.SetException(new ObjectDisposedException(nameof(AssistSessionController)));
    }

    return tcs.Task;
  }

  private async Task GenerateNewCodeCoreAsync()
  {
    if (State is not AssistSessionState.Ready and not AssistSessionState.PendingConsent)
    {
      return;
    }

    SetConsentPending(null, null, null, null);
    await EndCurrentSessionAsync().ConfigureAwait(false);
    await CreateSessionAsync(CancellationToken.None).ConfigureAwait(false);
  }

  private async Task HandleConnectionLostCoreAsync()
  {
    if (State == AssistSessionState.Disconnected)
    {
      return;
    }

    _logger.LogWarning("Assist hub connection lost. Reconnection was not possible.");
    await StopSessionHostAsync().ConfigureAwait(false);
    _activeSessionId = null;
    Code = null;
    _connectedServerUri = null;
    SetConsentPending(null, null, null, null);
    StatusMessage = Localization.StatusConnectionLost;
    SetState(AssistSessionState.Disconnected);
    RaiseStateChanged();
  }

  private Task HandleConsentCancelledCoreAsync(AssistConsentCancelledDto dto)
  {
    if (State != AssistSessionState.PendingConsent || PendingSessionId != dto.SessionId)
    {
      return Task.CompletedTask;
    }

    _logger.LogInformation("The consent request for assist session {SessionId} was cancelled by the technician.", dto.SessionId);
    SetConsentPending(null, null, null, null);
    StatusMessage = Localization.StatusConsentCancelled;
    SetState(AssistSessionState.Ready);
    RaiseStateChanged();
    return Task.CompletedTask;
  }

  private Task HandleConsentRequestedCoreAsync(AssistConsentRequestedDto dto)
  {
    if (_activeSessionId is not Guid activeSessionId || dto.SessionId != activeSessionId)
    {
      return Task.CompletedTask;
    }

    SetConsentPending(dto.SessionId, dto.ViewerDisplayName, dto.ViewerEmail, dto.ConsentNonce);
    StatusMessage = null;
    SetState(AssistSessionState.PendingConsent);
    RaiseStateChanged();
    return Task.CompletedTask;
  }

  private Task HandleConsentTimedOutCoreAsync(AssistConsentTimedOutDto dto)
  {
    if (State != AssistSessionState.PendingConsent || PendingSessionId != dto.SessionId)
    {
      return Task.CompletedTask;
    }

    _logger.LogInformation("The consent request for assist session {SessionId} timed out.", dto.SessionId);
    SetConsentPending(null, null, null, null);
    StatusMessage = Localization.StatusConsentTimedOut;
    SetState(AssistSessionState.Ready);
    RaiseStateChanged();
    return Task.CompletedTask;
  }

  private async Task HandleReconnectedCoreAsync()
  {
    _logger.LogInformation("Assist hub reconnected. Generating a fresh code.");

    await EndCurrentSessionAsync().ConfigureAwait(false);
    await StopSessionHostAsync().ConfigureAwait(false);
    SetConsentPending(null, null, null, null);
    StatusMessage = Localization.StatusReconnected;
    await CreateSessionCoreAsync(CancellationToken.None).ConfigureAwait(false);
    RaiseStateChanged();
  }

  private async Task HandleSessionEndedCoreAsync(AssistSessionEndedDto dto)
  {
    if (_activeSessionId is not Guid activeSessionId || dto.SessionId != activeSessionId)
    {
      return;
    }

    await StopSessionHostAsync().ConfigureAwait(false);
    SetConsentPending(null, null, null, null);
    StatusMessage = dto.Reason;
    await CreateSessionCoreAsync(CancellationToken.None).ConfigureAwait(false);
    RaiseStateChanged();
  }

  private async Task HandleSessionHostStoppedCoreAsync(Guid sessionId)
  {
    if (_activeSessionId != sessionId)
    {
      return;
    }

    _sessionHost = null;
    StatusMessage = Localization.StatusSessionEnded;
    await EndCurrentSessionAsync().ConfigureAwait(false);
    SetConsentPending(null, null, null, null);
    await CreateSessionCoreAsync(CancellationToken.None).ConfigureAwait(false);
    RaiseStateChanged();
  }

  private async Task HandleSessionStartedCoreAsync(AssistSessionStartedDto dto)
  {
    if (_activeSessionId is not Guid activeSessionId || dto.SessionId != activeSessionId)
    {
      return;
    }

    if (_connectedServerUri is not { } serverUri)
    {
      return;
    }

    SetConsentPending(null, null, null, null);
    StatusMessage = string.Format(Localization.StatusConnectedToViewer, dto.ViewerDisplayName);
    SetState(AssistSessionState.Active);
    RaiseStateChanged();

    await StartSessionHostAsync(new AssistSessionRequest(
      serverUri,
      dto.SessionId,
      dto.AccessToken,
      dto.ViewerDisplayName)).ConfigureAwait(false);
  }

  private async Task<THubResult> InvokeHubAsync<THubResult>(Func<CancellationToken, Task<THubResult>> invoke)
  {
    // Bounded so a wedged server can never stall the work queue behind us.
    using var cts = new CancellationTokenSource(_invokeTimeout);
    return await invoke(cts.Token).ConfigureAwait(false);
  }

  private void Post(Func<Task> work)
  {
    if (!_workQueue.Writer.TryWrite(work))
    {
      _logger.LogWarning("Dropped an assist work item after the controller was disposed.");
    }
  }

  private async Task ProcessQueueAsync()
  {
    await foreach (var work in _workQueue.Reader.ReadAllAsync().ConfigureAwait(false))
    {
      try
      {
        await work().ConfigureAwait(false);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Unexpected error while processing an assist work item.");
        StatusMessage = Localization.StatusUnexpectedError;
        RaiseStateChanged();
      }
    }
  }

  private void RaiseStateChanged() => StateChanged?.Invoke();

  private async Task RespondToConsentCoreAsync(bool approved)
  {
    if (PendingSessionId is not Guid sessionId)
    {
      return;
    }

    var consentNonce = PendingConsentNonce ?? Guid.Empty;
    var result = await InvokeHubAsync(ct => _hubClient.RespondToAssistConsent(sessionId, approved, consentNonce, ct));
    if (!result.IsSuccess)
    {
      StatusMessage = result.Reason ?? Localization.StatusConsentResponseFailed;
      await GenerateNewCodeCoreAsync().ConfigureAwait(false);
      return;
    }

    if (!approved)
    {
      await GenerateNewCodeCoreAsync().ConfigureAwait(false);
    }
  }

  private void SetConsentPending(Guid? sessionId, string? viewerName, string? email, Guid? consentNonce)
  {
    PendingSessionId = sessionId;
    PendingConsentViewerName = viewerName;
    PendingConsentEmail = email;
    PendingConsentNonce = consentNonce;
  }

  private void SetState(AssistSessionState state, string? statusMessage = null)
  {
    // A null status means "keep the message the caller already set", so failure messages written
    // before transitioning to Disconnected are not wiped out.
    if (statusMessage is not null)
    {
      StatusMessage = statusMessage;
    }

    State = state;
    RaiseStateChanged();
  }

  private async Task StartSessionHostAsync(AssistSessionRequest request)
  {
    try
    {
      var host = _sessionHostFactory.CreateSessionHost(request);
      var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();

      _ = Task.Run(async () =>
      {
        var stopped = new TaskCompletionSource();
        using var registration = lifetime.ApplicationStopping.Register(stopped.SetResult);

        try
        {
          await stopped.Task.ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
          return;
        }

        // Skip the handoff when this side already tore the host down; that path finished
        // the session bookkeeping itself.
        if (!ReferenceEquals(_sessionHost, host) && _sessionHost is null)
        {
          return;
        }

        await EnqueueAsync(() => HandleSessionHostStoppedCoreAsync(request.SessionId)).ConfigureAwait(false);
      });

      // Publish the host before starting it. If the host faults during StartAsync, the stopped
      // watcher still recognizes it and tears the session down instead of stranding it active.
      _sessionHost = host;
      await host.StartAsync().ConfigureAwait(false);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to start the assist session host.");
      StatusMessage = Localization.StatusSessionStartFailed;
      await EndCurrentSessionAsync().ConfigureAwait(false);
      await CreateSessionCoreAsync(CancellationToken.None).ConfigureAwait(false);
      RaiseStateChanged();
    }
  }

  private async Task StopSessionHostAsync()
  {
    var host = Interlocked.Exchange(ref _sessionHost, null);
    if (host is null)
    {
      return;
    }

    try
    {
      using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
      await host.StopAsync(cts.Token).ConfigureAwait(false);
    }
    catch (Exception ex)
    {
      _logger.LogWarning(ex, "Error while stopping the assist session host.");
    }
    finally
    {
      host.Dispose();
    }
  }
}
