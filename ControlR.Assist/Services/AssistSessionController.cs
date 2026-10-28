using ControlR.DesktopClient.Common.ServiceInterfaces;
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
/// capture host.
/// </summary>
public sealed class AssistSessionController(
  IAssistSignalRClient hubClient,
  IAssistSessionHostFactory sessionHostFactory,
  IAssistSettingsService settingsService,
  ILogger<AssistSessionController> logger) : IAsyncDisposable
{
  private readonly IAssistSignalRClient _hubClient = hubClient;
  private readonly ILogger<AssistSessionController> _logger = logger;
  private readonly IAssistSessionHostFactory _sessionHostFactory = sessionHostFactory;
  private readonly IAssistSettingsService _settingsService = settingsService;
  private readonly SemaphoreSlim _stateLock = new(1, 1);

  private Guid? _activeSessionId;
  private Uri? _connectedServerUri;
  private IHost? _sessionHost;

  public event Action? StateChanged;

  public string? Code { get; private set; }
  public bool IsInputBlocked { get; private set; }
  public string? PendingConsentEmail { get; private set; }
  public string? PendingConsentViewerName { get; private set; }
  public Guid? PendingSessionId { get; private set; }
  public AssistSessionState State { get; private set; } = AssistSessionState.Disconnected;
  public string? StatusMessage { get; private set; }

  public async Task<bool> ConnectAsync(string serverUrl, CancellationToken cancellationToken)
  {
    if (!TryNormalizeServerUri(serverUrl, out var serverUri))
    {
      StatusMessage = Localization.StatusInvalidUrl;
      RaiseStateChanged();
      return false;
    }

    await TransitionAsync(AssistSessionState.Connecting, Localization.StatusConnecting);

    if (!await _hubClient.ConnectAsync(serverUri, cancellationToken))
    {
      StatusMessage = Localization.StatusUnreachable;
      await TransitionAsync(AssistSessionState.Disconnected);
      return false;
    }

    var savedSettings = _settingsService.GetSettings();
    AssistSettingsService.RememberUrl(savedSettings, serverUri.ToString());
    await _settingsService.SaveAsync(savedSettings, cancellationToken);

    _connectedServerUri = serverUri;
    var created = await CreateSessionAsync(cancellationToken);
    if (!created)
    {
      await _hubClient.DisconnectAsync();
      _connectedServerUri = null;
      await TransitionAsync(AssistSessionState.Disconnected);
      return false;
    }

    return true;
  }

  public async Task DisconnectAsync()
  {
    await _stateLock.WaitAsync();

    try
    {
      await EndCurrentSessionAsync();
      await StopSessionHostAsync();
      await _hubClient.DisconnectAsync();
      _connectedServerUri = null;
      Code = null;
      SetConsentPending(null, null, null);
      StatusMessage = null;
      SetState(AssistSessionState.Disconnected);
    }
    finally
    {
      _stateLock.Release();
      RaiseStateChanged();
    }
  }

  public async ValueTask DisposeAsync()
  {
    await StopSessionHostAsync();
    await _hubClient.DisposeAsync();
    _stateLock.Dispose();
  }

  public async Task GenerateNewCodeAsync()
  {
    await _stateLock.WaitAsync();

    try
    {
      if (State is not AssistSessionState.Ready and not AssistSessionState.PendingConsent)
      {
        return;
      }

      SetConsentPending(null, null, null);
      await EndCurrentSessionAsync();
      await CreateSessionAsync(CancellationToken.None);
    }
    finally
    {
      _stateLock.Release();
      RaiseStateChanged();
    }
  }

  public async Task HandleConsentRequested(AssistConsentRequestedDto dto)
  {
    await _stateLock.WaitAsync();

    try
    {
      if (_activeSessionId is not Guid activeSessionId || dto.SessionId != activeSessionId)
      {
        return;
      }

      SetConsentPending(dto.SessionId, dto.ViewerDisplayName, dto.ViewerEmail);
      StatusMessage = null;
      SetState(AssistSessionState.PendingConsent);
    }
    finally
    {
      _stateLock.Release();
      RaiseStateChanged();
    }
  }

  public async Task HandleReconnected()
  {
    _logger.LogInformation("Assist hub reconnected. Generating a fresh code.");

    await _stateLock.WaitAsync();

    try
    {
      await EndCurrentSessionAsync();
      await StopSessionHostAsync();
      SetConsentPending(null, null, null);
      StatusMessage = Localization.StatusReconnected;
      await CreateSessionCoreAsync(CancellationToken.None);
    }
    finally
    {
      _stateLock.Release();
      RaiseStateChanged();
    }
  }

  public async Task HandleSessionEnded(AssistSessionEndedDto dto)
  {
    await _stateLock.WaitAsync();

    try
    {
      if (_activeSessionId is not Guid activeSessionId || dto.SessionId != activeSessionId)
      {
        return;
      }

      await StopSessionHostAsync();
      SetConsentPending(null, null, null);
      StatusMessage = dto.Reason;
      await CreateSessionCoreAsync(CancellationToken.None);
    }
    finally
    {
      _stateLock.Release();
      RaiseStateChanged();
    }
  }

  public async Task HandleSessionStarted(AssistSessionStartedDto dto)
  {
    await _stateLock.WaitAsync();

    try
    {
      if (_activeSessionId is not Guid activeSessionId || dto.SessionId != activeSessionId)
      {
        return;
      }

      if (_connectedServerUri is not { } serverUri)
      {
        return;
      }

      SetConsentPending(null, null, null);
      StatusMessage = string.Format(Localization.StatusConnectedToViewer, dto.ViewerDisplayName);
      SetState(AssistSessionState.Active);
      RaiseStateChanged();

      await StartSessionHostAsync(new AssistSessionRequest(
        serverUri,
        dto.SessionId,
        dto.AccessToken,
        dto.ViewerDisplayName));
    }
    finally
    {
      _stateLock.Release();
    }
  }

  public async Task RespondToConsentAsync(bool approved)
  {
    if (PendingSessionId is not Guid sessionId)
    {
      return;
    }

    var result = await _hubClient.RespondToAssistConsent(sessionId, approved, CancellationToken.None);
    if (!result.IsSuccess)
    {
      StatusMessage = result.Reason ?? Localization.StatusConsentResponseFailed;
      await GenerateNewCodeAsync();
      return;
    }

    if (!approved)
    {
      await GenerateNewCodeAsync();
    }
  }

  public async Task ToggleBlockInputAsync()
  {
    if (_sessionHost is not { } host)
    {
      return;
    }

    try
    {
      var inputSimulator = host.Services.GetRequiredService<IInputSimulator>();
      IsInputBlocked = await inputSimulator.SetBlockInput(!IsInputBlocked);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to toggle local input blocking.");
    }

    RaiseStateChanged();
  }

  private static bool TryNormalizeServerUri(string? input, out Uri serverUri)
  {
    serverUri = null!;

    var candidate = (input ?? string.Empty).Trim();
    if (candidate.Length == 0)
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

    serverUri = parsed;
    return true;
  }

  private async Task<bool> CreateSessionAsync(CancellationToken cancellationToken)
  {
    var result = await CreateSessionCoreAsync(cancellationToken);
    RaiseStateChanged();
    return result;
  }

  private async Task<bool> CreateSessionCoreAsync(CancellationToken cancellationToken)
  {
    var result = await _hubClient.CreateAssistSession(cancellationToken);
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

  private async Task EndCurrentSessionAsync()
  {
    if (_activeSessionId is not Guid sessionId)
    {
      return;
    }

    _ = await _hubClient.EndAssistSession(sessionId, CancellationToken.None);
    _activeSessionId = null;
  }

  private async Task HandleSessionHostStopped(Guid sessionId)
  {
    await _stateLock.WaitAsync();

    try
    {
      if (_activeSessionId != sessionId)
      {
        return;
      }

      _sessionHost = null;
      StatusMessage = Localization.StatusSessionEnded;
      await EndCurrentSessionAsync();
      SetConsentPending(null, null, null);
      await CreateSessionCoreAsync(CancellationToken.None);
    }
    finally
    {
      _stateLock.Release();
      RaiseStateChanged();
    }
  }

  private void RaiseStateChanged() => StateChanged?.Invoke();

  private void SetConsentPending(Guid? sessionId, string? viewerName, string? email)
  {
    PendingSessionId = sessionId;
    PendingConsentViewerName = viewerName;
    PendingConsentEmail = email;
  }

  private void SetState(AssistSessionState state) => State = state;

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
          await stopped.Task;
        }
        catch (ObjectDisposedException)
        {
          return;
        }

        // Skip the handoff when this side already tore the host down while holding the lock;
        // that path finished the session bookkeeping itself.
        if (!ReferenceEquals(_sessionHost, host) && _sessionHost is null)
        {
          return;
        }

        await HandleSessionHostStopped(request.SessionId);
      });

      await host.StartAsync();
      _sessionHost = host;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to start the assist session host.");
      StatusMessage = Localization.StatusSessionStartFailed;
      await EndCurrentSessionAsync();
      await CreateSessionCoreAsync(CancellationToken.None);
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
      await host.StopAsync(cts.Token);
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

  private async Task TransitionAsync(AssistSessionState state, string? statusMessage = null)
  {
    StatusMessage = statusMessage;
    SetState(state);
    await Task.CompletedTask;
    RaiseStateChanged();
  }
}
