using System.Diagnostics;
using ControlR.DesktopClient.Common.Options;
using ControlR.Libraries.Shared.Primitives;
using ControlR.Libraries.Shared.Services.FileSystem;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.Threading;
using Microsoft.Win32.SafeHandles;
using Tmds.DBus;

namespace ControlR.DesktopClient.Linux.XdgPortal;

public interface IXdgDesktopPortal : IDisposable
{
  void DeleteRestoreToken();
  Task<string?> GetClipboardText(CancellationToken cancellationToken);
  Task<(SafeFileHandle Fd, string SessionHandle)?> GetPipeWireConnection();
  Task<string?> GetRemoteDesktopSessionHandle(CancellationToken cancellationToken = default);
  Task<List<PipeWireStreamInfo>> GetScreenCastStreams();
  Task Initialize(bool bypassRestoreToken = false, CancellationToken cancellationToken = default);
  Task NotifyKeyboardKeycode(string sessionHandle, int keycode, bool pressed);
  Task NotifyKeyboardKeysym(string sessionHandle, int keysym, bool pressed);
  Task NotifyPointerAxis(string sessionHandle, double dx, double dy, bool finish = true);
  Task NotifyPointerAxisDiscrete(string sessionHandle, uint axis, int steps);
  Task NotifyPointerButton(string sessionHandle, int button, bool pressed);
  Task NotifyPointerMotion(string sessionHandle, double dx, double dy);
  Task NotifyPointerMotionAbsolute(string sessionHandle, uint stream, double x, double y);
  Task<bool> ProbeRestoreToken(string restoreToken, CancellationToken cancellationToken = default);
  Task<bool> RequestRemoteDesktopPermission(bool bypassRestoreToken = false, CancellationToken cancellationToken = default);
  Task SetClipboardText(string text, CancellationToken cancellationToken);
}

public sealed class XdgDesktopPortal(
  IFileSystem fileSystem,
  IFileAccessPermissions fileAccessPermissions,
  IOptionsMonitor<DesktopClientOptions> options,
  ILogger<XdgDesktopPortal> logger) : IXdgDesktopPortal, IDisposable
{
  private const string PortalBusName = "org.freedesktop.portal.Desktop";
  private const string PortalObjectPath = "/org/freedesktop/portal/desktop";

  private static readonly string[] _clipboardMimeTypes = ["text/plain;charset=utf-8"];
  private static readonly TimeSpan _defaultProbeTimeout = Debugger.IsAttached ? TimeSpan.FromSeconds(120) : TimeSpan.FromSeconds(5);

  private readonly IFileAccessPermissions _fileAccessPermissions = fileAccessPermissions;
  private readonly IFileSystem _fileSystem = fileSystem;
  private readonly SemaphoreSlim _initLock = new(1, 1);
  private readonly ILogger<XdgDesktopPortal> _logger = logger;
  private readonly IOptionsMonitor<DesktopClientOptions> _options = options;
  private readonly TimeSpan _userInteractionTimeout = TimeSpan.FromSeconds(90);

  private bool _clipboardEnabled;
  private Connection? _connection;
  private ConnectionInfo? _connectionInfo;
  private bool _disposed;
  private bool _initialized;
  private volatile string? _ownedClipboardText;
  private SafeFileHandle? _pipewireFd;
  private IDisposable? _selectionOwnerChangedSubscription;
  private IDisposable? _selectionTransferSubscription;
  private string? _sessionHandle;
  private volatile bool _sessionOwnsClipboard;
  private List<PipeWireStreamInfo>? _streams;

  private Connection Connection => _connection
    ?? throw new InvalidOperationException("DBus connection is not established.");
  private ConnectionInfo ConnectionInfo => _connectionInfo
    ?? throw new InvalidOperationException("DBus connection is not established.");

  public void DeleteRestoreToken()
  {
    try
    {
      var instanceId = _options.CurrentValue.InstanceId;
      var tokenPath = PathConstants.GetWaylandRemoteDesktopRestoreTokenPath(instanceId);
      if (_fileSystem.FileExists(tokenPath))
      {
        _fileSystem.DeleteFile(tokenPath);
        _logger.LogInformation("Deleted stale restore token");
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to delete restore token");
    }
  }

  public void Dispose()
  {
    if (_disposed) return;

    _disposed = true;

    _selectionOwnerChangedSubscription?.Dispose();
    _selectionTransferSubscription?.Dispose();

    _pipewireFd?.Dispose();
    _connection?.Dispose();
    _initLock?.Dispose();

    _clipboardEnabled = false;
    _ownedClipboardText = null;
    _pipewireFd = null;
    _connection = null;
    _connectionInfo = null;
    _selectionOwnerChangedSubscription = null;
    _selectionTransferSubscription = null;
    _sessionOwnsClipboard = false;
    _sessionHandle = null;
    _streams = null;
  }

  public async Task<string?> GetClipboardText(CancellationToken cancellationToken)
  {
    try
    {
      await EnsureInitializedAsync(cancellationToken: cancellationToken);

      if (!_clipboardEnabled || _sessionHandle is null)
      {
        return null;
      }

      if (_sessionOwnsClipboard)
      {
        return _ownedClipboardText;
      }

      var proxy = Connection.CreateProxy<IClipboard>(PortalBusName, PortalObjectPath);
      var fd = await proxy.SelectionReadAsync(
        new ObjectPath(_sessionHandle),
        "text/plain;charset=utf-8")
        .WithCancellation(cancellationToken)
        ;

      using var stream = new FileStream(fd, FileAccess.Read);
      using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);
      return await reader
        .ReadToEndAsync(cancellationToken)
        ;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error reading clipboard text via portal");
      return null;
    }
  }

  public async Task<(SafeFileHandle Fd, string SessionHandle)?> GetPipeWireConnection()
  {
    await EnsureInitializedAsync();
    return _pipewireFd != null && _sessionHandle != null
    ? (_pipewireFd, _sessionHandle)
    : throw new InvalidOperationException("PipeWire connection is not initialized.");
  }

  public async Task<string?> GetRemoteDesktopSessionHandle(CancellationToken cancellationToken = default)
  {
    await EnsureInitializedAsync(cancellationToken: cancellationToken);
    return _sessionHandle;
  }

  public async Task<List<PipeWireStreamInfo>> GetScreenCastStreams()
  {
    await EnsureInitializedAsync();
    return _streams ?? throw new InvalidOperationException("ScreenCast streams are not initialized.");
  }

  public async Task Initialize(bool bypassRestoreToken = false, CancellationToken cancellationToken = default)
  {
    await EnsureInitializedAsync(bypassRestoreToken);
  }

  public async Task NotifyKeyboardKeycode(string sessionHandle, int keycode, bool pressed)
  {
    try
    {
      await EnsureInitializedAsync();
      var proxy = Connection.CreateProxy<IRemoteDesktop>(PortalBusName, PortalObjectPath);
      await proxy.NotifyKeyboardKeycodeAsync(new ObjectPath(sessionHandle), new Dictionary<string, object>(), keycode, pressed ? 1u : 0u);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error sending keyboard keycode");
    }
  }

  public async Task NotifyKeyboardKeysym(string sessionHandle, int keysym, bool pressed)
  {
    try
    {
      await EnsureInitializedAsync();
      var proxy = Connection.CreateProxy<IRemoteDesktop>(PortalBusName, PortalObjectPath);
      await proxy.NotifyKeyboardKeysymAsync(new ObjectPath(sessionHandle), new Dictionary<string, object>(), keysym, pressed ? 1u : 0u);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error sending keyboard keysym");
    }
  }

  public async Task NotifyPointerAxis(string sessionHandle, double dx, double dy, bool finish = true)
  {
    try
    {
      await EnsureInitializedAsync();
      var options = new Dictionary<string, object> { ["finish"] = finish };
      var proxy = Connection.CreateProxy<IRemoteDesktop>(PortalBusName, PortalObjectPath);
      await proxy.NotifyPointerAxisAsync(new ObjectPath(sessionHandle), options, dx, dy);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error sending pointer axis");
    }
  }

  public async Task NotifyPointerAxisDiscrete(string sessionHandle, uint axis, int steps)
  {
    try
    {
      await EnsureInitializedAsync();
      var proxy = Connection.CreateProxy<IRemoteDesktop>(PortalBusName, PortalObjectPath);
      await proxy.NotifyPointerAxisDiscreteAsync(new ObjectPath(sessionHandle), new Dictionary<string, object>(), axis, steps);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error sending discrete pointer axis");
    }
  }

  public async Task NotifyPointerButton(string sessionHandle, int button, bool pressed)
  {
    try
    {
      await EnsureInitializedAsync();
      var proxy = Connection.CreateProxy<IRemoteDesktop>(PortalBusName, PortalObjectPath);
      await proxy.NotifyPointerButtonAsync(new ObjectPath(sessionHandle), new Dictionary<string, object>(), button, pressed ? 1u : 0u);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error sending pointer button");
    }
  }

  public async Task NotifyPointerMotion(string sessionHandle, double dx, double dy)
  {
    try
    {
      await EnsureInitializedAsync();
      var proxy = Connection.CreateProxy<IRemoteDesktop>(PortalBusName, PortalObjectPath);
      await proxy.NotifyPointerMotionAsync(
        new ObjectPath(sessionHandle),
        new Dictionary<string, object>(),
        dx,
        dy);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error sending pointer motion");
    }
  }

  public async Task NotifyPointerMotionAbsolute(string sessionHandle, uint stream, double x, double y)
  {
    try
    {
      await EnsureInitializedAsync();
      var proxy = Connection.CreateProxy<IRemoteDesktop>(PortalBusName, PortalObjectPath);
      await proxy.NotifyPointerMotionAbsoluteAsync(
        new ObjectPath(sessionHandle),
        new Dictionary<string, object>(),
        stream,
        x,
        y);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error sending absolute pointer motion");
    }
  }

  public async Task<bool> ProbeRestoreToken(string restoreToken, CancellationToken cancellationToken = default)
  {
    if (_initialized)
    {
      _logger.LogInformation("Skipping Wayland restore token probe because the portal is already initialized.");
      return true;
    }

    try
    {
      _logger.LogInformation("Starting Wayland restore token probe.");

      using var cts = new CancellationTokenSource(_defaultProbeTimeout);
      using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cts.Token);

      var result = await StartRemoteDesktopSession(
        restoreToken,
        openPipeWireRemote: false,
        cancellationToken: combinedCts.Token);

      if (!result.IsSuccess)
      {
        _logger.LogWarning("Probe failed: restore token is stale. {Error}", result.Reason);
        return false;
      }

      _logger.LogInformation("Probe succeeded: restore token is valid and was rotated.");
      return true;
    }
    catch (OperationCanceledException)
    {
      _logger.LogWarning("Wayland restore token probe timed out or was canceled.");
      return false;
    }
    catch (Exception ex)
    {
      _logger.LogWarning(ex, "Probe failed with exception");
      return false;
    }
  }

  public async Task<bool> RequestRemoteDesktopPermission(bool bypassRestoreToken = false, CancellationToken cancellationToken = default)
  {
    try
    {
      _logger.LogInformation(
        "Requesting Wayland remote desktop permission. BypassRestoreToken={BypassRestoreToken}",
        bypassRestoreToken);

      var restoreToken = GetRestoreTokenForSession(bypassRestoreToken);
      var result = await StartRemoteDesktopSession(
        restoreToken,
        openPipeWireRemote: false,
        cancellationToken: cancellationToken);

      if (!result.IsSuccess)
      {
        _logger.LogWarning("Wayland remote desktop permission request failed. {Error}", result.Reason);
        return false;
      }

      _logger.LogInformation("Wayland remote desktop permission request completed successfully.");
      return true;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error requesting Wayland remote desktop permission");
      return false;
    }
  }

  public async Task SetClipboardText(string text, CancellationToken cancellationToken)
  {
    var previousClipboardText = _ownedClipboardText;
    var previousSessionOwnsClipboard = _sessionOwnsClipboard;

    try
    {
      await EnsureInitializedAsync(cancellationToken: cancellationToken);

      if (!_clipboardEnabled || _sessionHandle is null)
      {
        _logger.LogWarning(
          "Cannot set clipboard text. Clipboard Enabled: {ClipboardEnabled}, Session Handle Available: {SessionHandle}",
          _clipboardEnabled,
          _sessionHandle is not null);

        return;
      }

      _ownedClipboardText = text;

      _logger.LogDebug(
        "Advertising clipboard selection for session {SessionHandle}. TextLength={TextLength}",
        _sessionHandle,
        text.Length);

      var proxy = Connection.CreateProxy<IClipboard>(PortalBusName, PortalObjectPath);

      await proxy.SetSelectionAsync(
        new ObjectPath(_sessionHandle),
        new Dictionary<string, object>
        {
          ["mime_types"] = _clipboardMimeTypes
        })
        .WithCancellation(cancellationToken);

      _logger.LogDebug(
        "Clipboard selection advertisement completed for session {SessionHandle}",
        _sessionHandle);

    }
    catch (OperationCanceledException ex)
    {
      if (!_sessionOwnsClipboard)
      {
        _ownedClipboardText = previousClipboardText;
        _sessionOwnsClipboard = previousSessionOwnsClipboard;
      }

      _logger.LogError(ex, "Error setting clipboard text via portal");
    }
    catch (Exception ex)
    {
      if (!_sessionOwnsClipboard)
      {
        _ownedClipboardText = previousClipboardText;
        _sessionOwnsClipboard = previousSessionOwnsClipboard;
      }

      _logger.LogError(ex, "Error setting clipboard text via portal");
    }
  }

  private async Task CompleteSelectionTransfer(ObjectPath sessionHandle, uint serial, bool success)
  {
    if (_disposed || _connection is null)
    {
      return;
    }

    try
    {
      var proxy = Connection.CreateProxy<IClipboard>(PortalBusName, PortalObjectPath);
      await proxy.SelectionWriteDoneAsync(sessionHandle, serial, success);

      _logger.LogDebug(
        "Completed clipboard SelectionTransfer. Serial={Serial}, Success={Success}",
        serial,
        success);
    }
    catch (ObjectDisposedException)
    {
      _logger.LogDebug(
        "Skipping clipboard SelectionTransfer completion because the DBus connection is disposed. Serial={Serial}",
        serial);
    }
    catch (Exception ex)
    {
      _logger.LogError(
        ex,
        "Failed to complete clipboard SelectionTransfer. Serial={Serial}, Success={Success}",
        serial,
        success);
    }
  }

  private async Task ConnectAsync(CancellationToken cancellationToken = default)
  {
    try
    {
      _connection = new Connection(Address.Session);
      _connectionInfo = await _connection.ConnectAsync()
        .WithCancellation(cancellationToken);

      _logger.LogDebug("Connected to DBus session bus");
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to connect to DBus session bus");
      throw;
    }
  }

  private async Task<Result<string>> CreateRemoteDesktopSession(CancellationToken cancellationToken = default)
  {
    try
    {
      var sessionToken = $"controlr_session_{Guid.NewGuid():N}";
      var requestToken = $"controlr_request_{Guid.NewGuid():N}";
      var expectedRequestPath = GetExpectedRequestPath(requestToken);

      var options = new Dictionary<string, object>
      {
        ["handle_token"] = requestToken,
        ["session_handle_token"] = sessionToken
      };

      var proxy = Connection.CreateProxy<IRemoteDesktop>(PortalBusName, PortalObjectPath);

      var (response, results) = await WaitForResponse(
          expectedRequestPath,
          () => proxy.CreateSessionAsync(options),
          cancellationToken
        )
        ;

      if (response != 0)
      {
        return Result.Fail<string>($"RemoteDesktop session creation failed with response code {response}");
      }

      if (results.TryGetValue("session_handle", out var sessionHandle) && sessionHandle is string handle)
      {
        _logger.LogInformation("Created RemoteDesktop session: {SessionHandle}", handle);
        return Result.Ok(handle);
      }

      return Result.Fail<string>("Session creation succeeded but no session handle returned");
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error creating RemoteDesktop session");
      return Result.Fail<string>($"Exception creating session: {ex.Message}");
    }
  }

  private async Task EnsureInitializedAsync(bool bypassRestoreToken = false, CancellationToken cancellationToken = default)
  {
    if (_initialized)
    {
      return;
    }

    await _initLock.WaitAsync(cancellationToken);
    try
    {
      if (_initialized)
      {
        return;
      }

      _logger.LogInformation(
        "Initializing Wayland desktop portal. BypassRestoreToken={BypassRestoreToken}",
        bypassRestoreToken);

      var restoreToken = GetRestoreTokenForSession(bypassRestoreToken);
      var result = await StartRemoteDesktopSession(
        restoreToken,
        openPipeWireRemote: true,
        cancellationToken: cancellationToken);

      if (!result.IsSuccess || result.Value is null)
      {
        _logger.LogError("Wayland desktop portal initialization failed. {Error}", result.Reason);
        return;
      }

      _sessionHandle = result.Value.SessionHandle;
      _streams = result.Value.Streams;
      _pipewireFd = result.Value.PipeWireFd;
      _clipboardEnabled = result.Value.ClipboardEnabled;
      _initialized = true;

      if (_clipboardEnabled && _sessionHandle is not null)
      {
        await SetupClipboardSignalHandlers();
      }

      _logger.LogInformation("Wayland desktop portal initialization completed successfully.");
    }
    finally
    {
      _initLock.Release();
    }
  }

  private string GetExpectedRequestPath(string requestToken)
  {
    var senderName = ConnectionInfo.LocalName.TrimStart(':').Replace('.', '_');
    return $"/org/freedesktop/portal/desktop/request/{senderName}/{requestToken}";
  }

  private string? GetRestoreTokenForSession(bool bypassRestoreToken)
  {
    if (bypassRestoreToken)
    {
      return null;
    }

    var restoreToken = LoadRestoreToken();
    if (!string.IsNullOrWhiteSpace(restoreToken))
    {
      _logger.LogInformation("Using saved restore token");
    }

    return restoreToken;
  }

  private void HandleSelectionOwnerChanged((ObjectPath SessionHandle, IDictionary<string, object> Options) data)
  {
    try
    {
      if (_disposed)
      {
        return;
      }

      if (_sessionHandle is null || data.SessionHandle != new ObjectPath(_sessionHandle))
      {
        return;
      }

      if (!data.Options.TryGetValue("session_is_owner", out var ownerObj) || ownerObj is not bool sessionIsOwner)
      {
        _logger.LogDebug("Clipboard SelectionOwnerChanged did not include session_is_owner.");
        return;
      }

      _sessionOwnsClipboard = sessionIsOwner;

      if (!sessionIsOwner)
      {
        _ownedClipboardText = null;
      }

      _logger.LogDebug(
        "Clipboard ownership changed. SessionOwnsClipboard={SessionOwnsClipboard}",
        sessionIsOwner);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error handling SelectionOwnerChanged signal");
    }
  }

  private async Task HandleSelectionTransfer((ObjectPath SessionHandle, string MimeType, uint Serial) data)
  {
    var success = false;

    try
    {
      if (_disposed)
      {
        return;
      }

      var proxy = Connection.CreateProxy<IClipboard>(PortalBusName, PortalObjectPath);
      var sessionPath = data.SessionHandle;

      if (data.MimeType is not ("text/plain;charset=utf-8" or "text/plain"))
      {
        _logger.LogDebug(
          "Rejecting clipboard SelectionTransfer for unsupported mime type {MimeType}. Serial={Serial}",
          data.MimeType,
          data.Serial);
        return;
      }

      var text = _ownedClipboardText;
      if (text is null)
      {
        _logger.LogDebug(
          "Rejecting clipboard SelectionTransfer because no owned clipboard text is available. Serial={Serial}",
          data.Serial);
        return;
      }

      var fd = await proxy.SelectionWriteAsync(sessionPath, data.Serial);

      using var stream = new FileStream(fd, FileAccess.Write);
      using var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
      await writer.WriteAsync(text);
      await writer.FlushAsync();

      success = true;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error handling SelectionTransfer signal");
    }
    finally
    {
      await CompleteSelectionTransfer(data.SessionHandle, data.Serial, success);
    }
  }

  private string? LoadRestoreToken()
  {
    try
    {
      var instanceId = _options.CurrentValue.InstanceId;
      var tokenPath = PathConstants.GetWaylandRemoteDesktopRestoreTokenPath(instanceId);
      if (_fileSystem.FileExists(tokenPath))
      {
        return _fileSystem.ReadAllText(tokenPath).Trim();
      }
    }
    catch (Exception ex)
    {
      _logger.LogWarning(ex, "Failed to load restore token");
    }
    return null;
  }

  private async Task<Result<SafeFileHandle>> OpenPipeWireRemote(string sessionHandle)
  {
    try
    {
      var proxy = Connection.CreateProxy<IScreenCast>(PortalBusName, PortalObjectPath);
      var fd = await proxy.OpenPipeWireRemoteAsync(new ObjectPath(sessionHandle), new Dictionary<string, object>());

      _logger.LogInformation("Opened PipeWire remote with FD");
      return Result.Ok(fd);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error opening PipeWire remote");
      return Result.Fail<SafeFileHandle>($"Exception opening PipeWire remote: {ex.Message}");
    }
  }

  private void SaveRestoreToken(string token)
  {
    try
    {
      var instanceId = _options.CurrentValue.InstanceId;
      var tokenPath = PathConstants.GetWaylandRemoteDesktopRestoreTokenPath(instanceId);
      _fileSystem.WriteAllText(tokenPath, token);
      _fileAccessPermissions.Set(tokenPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to save restore token");
    }
  }

  private async Task<Result> SelectRemoteDesktopDevices(
    string sessionHandle,
    uint deviceTypes = 3,
    Dictionary<string, object>? additionalOptions = null,
    CancellationToken cancellationToken = default)
  {
    try
    {
      var requestToken = $"controlr_request_{Guid.NewGuid():N}";

      var options = new Dictionary<string, object>
      {
        ["handle_token"] = requestToken,
        ["types"] = deviceTypes
      };

      if (additionalOptions != null)
      {
        foreach (var kvp in additionalOptions)
        {
          options[kvp.Key] = kvp.Value;
        }
      }

      var expectedRequestPath = GetExpectedRequestPath(requestToken);
      var proxy = Connection.CreateProxy<IRemoteDesktop>(PortalBusName, PortalObjectPath);

      var (response, _) = await WaitForResponse(
          expectedRequestPath,
          () => proxy.SelectDevicesAsync(new ObjectPath(sessionHandle), options),
          cancellationToken: cancellationToken)
        ;

      if (response != 0)
      {
        return Result.Fail($"SelectDevices failed with response code {response}");
      }

      _logger.LogInformation("Successfully selected RemoteDesktop devices");
      return Result.Ok();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error selecting RemoteDesktop devices");
      return Result.Fail($"Exception selecting devices: {ex.Message}");
    }
  }

  private async Task<Result> SelectScreenCastSources(
    string sessionHandle,
    uint sourceTypes = 1,
    bool multipleSources = true,
    uint cursorMode = AvailableCursorModes.Embedded,
    Dictionary<string, object>? additionalOptions = null,
    CancellationToken cancellationToken = default)
  {
    try
    {
      var requestToken = $"controlr_request_{Guid.NewGuid():N}";

      var options = new Dictionary<string, object>
      {
        ["handle_token"] = requestToken,
        ["types"] = sourceTypes,
        ["multiple"] = multipleSources,
        ["cursor_mode"] = cursorMode
      };

      if (additionalOptions != null)
      {
        foreach (var kvp in additionalOptions)
        {
          options[kvp.Key] = kvp.Value;
        }
      }

      var expectedRequestPath = GetExpectedRequestPath(requestToken);
      var proxy = Connection.CreateProxy<IScreenCast>(PortalBusName, PortalObjectPath);

      var (response, _) = await WaitForResponse(
          expectedRequestPath,
          () => proxy.SelectSourcesAsync(new ObjectPath(sessionHandle), options),
          cancellationToken: cancellationToken)
        ;

      if (response != 0)
      {
        return Result.Fail($"SelectSources failed with response code {response}");
      }

      _logger.LogInformation("Successfully selected ScreenCast sources");
      return Result.Ok();
    }
    catch (OperationCanceledException)
    {
      _logger.LogWarning("ScreenCast source selection timed out or was canceled.");
      return Result.Fail("ScreenCast source selection was canceled.");
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error selecting ScreenCast sources");
      return Result.Fail($"Exception selecting sources: {ex.Message}");
    }
  }

  private async Task SetupClipboardSignalHandlers()
  {
    try
    {
      var proxy = Connection.CreateProxy<IClipboard>(PortalBusName, PortalObjectPath);

      _selectionOwnerChangedSubscription = await proxy.WatchSelectionOwnerChangedAsync(
        HandleSelectionOwnerChanged,
        ex =>
        {
          _logger.LogError(ex, "Error in SelectionOwnerChanged signal watcher");
        });

      _selectionTransferSubscription = await proxy.WatchSelectionTransferAsync(
        data => HandleSelectionTransfer(data).Forget(),
        ex =>
        {
          _logger.LogError(ex, "Error in SelectionTransfer signal watcher");
        });

      _logger.LogInformation("Clipboard signal handlers registered for session {Session}", _sessionHandle);
    }
    catch (Exception ex)
    {
      _logger.LogWarning(ex, "Failed to set up clipboard signal handlers. Clipboard write may not work.");
    }
  }

  private async Task<Result<RemoteDesktopStartResult>> StartRemoteDesktop(
    string sessionHandle,
    string parentWindow = "",
    CancellationToken cancellationToken = default)
  {
    try
    {
      var requestToken = $"controlr_request_{Guid.NewGuid():N}";

      var options = new Dictionary<string, object>
      {
        ["handle_token"] = requestToken
      };

      var expectedRequestPath = GetExpectedRequestPath(requestToken);
      var proxy = Connection.CreateProxy<IRemoteDesktop>(PortalBusName, PortalObjectPath);

      var (response, results) = await WaitForResponse(
          expectedRequestPath,
          () => proxy.StartAsync(new ObjectPath(sessionHandle), parentWindow, options),
          cancellationToken)
        ;

      if (response != 0)
      {
        return Result.Fail<RemoteDesktopStartResult>($"Start failed with response code {response}. User may have denied permission.");
      }

      string? restoreToken = null;

      if (results.TryGetValue("restore_token", out var tokenObj) && tokenObj is string token)
      {
        restoreToken = token;
      }

      if (results.TryGetValue("devices", out var devicesObj) && devicesObj is uint devices)
      {
        _logger.LogInformation("RemoteDesktop granted devices: {Devices}", devices);
      }

      var clipboardEnabled = false;
      if (results.TryGetValue("clipboard_enabled", out var clipboardObj) && clipboardObj is bool enabled)
      {
        clipboardEnabled = enabled;
        _logger.LogInformation("RemoteDesktop clipboard_enabled: {ClipboardEnabled}", clipboardEnabled);
      }

      var streams = new List<PipeWireStreamInfo>();

      if (results.TryGetValue("streams", out var streamsObj))
      {
        if (streamsObj is System.Collections.IEnumerable enumerable)
        {
          foreach (var entry in enumerable)
          {
            try
            {
              if (entry is ValueTuple<uint, IDictionary<string, object>> streamTuple)
              {
                var nodeId = streamTuple.Item1;
                var props = streamTuple.Item2;
                streams.Add(new PipeWireStreamInfo
                {
                  StreamIndex = streams.Count,
                  NodeId = nodeId,
                  Properties = new Dictionary<string, object>(props)
                });
              }
              else if (entry is System.Collections.IDictionary dict && dict.Contains(0) && dict.Contains(1))
              {
                var nodeId = Convert.ToUInt32(dict[0]);
                var props = dict[1] as IDictionary<string, object> ?? new Dictionary<string, object>();
                streams.Add(new PipeWireStreamInfo
                {
                  StreamIndex = streams.Count,
                  NodeId = nodeId,
                  Properties = new Dictionary<string, object>(props)
                });
              }
              else
              {
                var entryType = entry?.GetType();
                var fields = entryType?.GetFields() ?? [];
                if (fields.Length >= 2)
                {
                  var nodeId = Convert.ToUInt32(fields[0].GetValue(entry));
                  var propsObj = fields[1].GetValue(entry);
                  var props = propsObj as IDictionary<string, object> ?? new Dictionary<string, object>();
                  streams.Add(new PipeWireStreamInfo
                  {
                    StreamIndex = streams.Count,
                    NodeId = nodeId,
                    Properties = new Dictionary<string, object>(props)
                  });
                }
              }
            }
            catch (Exception parseEx)
            {
              _logger.LogWarning(parseEx, "Failed parsing a RemoteDesktop stream entry");
            }
          }
        }
      }
      var startResult = new RemoteDesktopStartResult()
      {
        Streams = streams,
        RestoreToken = restoreToken,
        ClipboardEnabled = clipboardEnabled
      };

      return Result.Ok(startResult);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error starting RemoteDesktop");
      return Result.Fail<RemoteDesktopStartResult>($"Exception starting RemoteDesktop: {ex.Message}");
    }
  }

  private async Task<Result<PortalSessionState>> StartRemoteDesktopSession(
    string? restoreToken,
    bool openPipeWireRemote,
    CancellationToken cancellationToken = default)
  {
    await ConnectAsync(cancellationToken);

    var sessionResult = await CreateRemoteDesktopSession(cancellationToken);
    if (!sessionResult.IsSuccess || sessionResult.Value is null)
    {
      return Result.Fail<PortalSessionState>(sessionResult.Reason ?? "Failed to create Wayland remote desktop session.");
    }

    var sessionHandle = sessionResult.Value;
    var remoteDesktopOptions = new Dictionary<string, object> { ["persist_mode"] = 2u };

    if (!string.IsNullOrWhiteSpace(restoreToken))
    {
      remoteDesktopOptions["restore_token"] = restoreToken;
    }

    var selectResult = await SelectRemoteDesktopDevices(
      sessionHandle,
      deviceTypes: 3,
      additionalOptions: remoteDesktopOptions,
      cancellationToken: cancellationToken)
      ;

    if (!selectResult.IsSuccess)
    {
      return Result.Fail<PortalSessionState>(selectResult.Reason ?? "Failed to select Wayland remote desktop devices.");
    }

    _logger.LogInformation("Wayland desktop portal selected remote desktop devices.");

    var selectSourcesResult = await SelectScreenCastSources(
      sessionHandle,
      sourceTypes: 1,
      multipleSources: true,
      cursorMode: AvailableCursorModes.Embedded,
      cancellationToken: cancellationToken);

    if (!selectSourcesResult.IsSuccess)
    {
      return Result.Fail<PortalSessionState>(selectSourcesResult.Reason ?? "Failed to select Wayland screen cast sources.");
    }

    _logger.LogInformation("Wayland desktop portal selected screen cast sources.");

    try
    {
      var clipboardProxy = Connection.CreateProxy<IClipboard>(PortalBusName, PortalObjectPath);
      await clipboardProxy.RequestClipboardAsync(
        new ObjectPath(sessionHandle),
        new Dictionary<string, object>());
      _logger.LogInformation("Requested clipboard access for Wayland desktop portal session.");
    }
    catch (Exception ex)
    {
      _logger.LogWarning(ex, "Failed to request clipboard access. Clipboard will not be available.");
    }

    var startResult = await StartRemoteDesktop(sessionHandle, cancellationToken: cancellationToken);
    if (!startResult.IsSuccess || startResult.Value is null)
    {
      return Result.Fail<PortalSessionState>(startResult.Reason ?? "Failed to start Wayland remote desktop session.");
    }

    _logger.LogInformation("Combined session started with {Count} stream(s)", startResult.Value.Streams.Count);

    if (!string.IsNullOrWhiteSpace(startResult.Value.RestoreToken))
    {
      SaveRestoreToken(startResult.Value.RestoreToken);
      _logger.LogInformation("Saved restore token");
    }

    SafeFileHandle? pipeWireFd = null;
    if (openPipeWireRemote)
    {
      var fdResult = await OpenPipeWireRemote(sessionHandle);
      if (!fdResult.IsSuccess || fdResult.Value is null)
      {
        return Result.Fail<PortalSessionState>(fdResult.Reason ?? "Failed to open PipeWire remote.");
      }

      pipeWireFd = fdResult.Value;
      _logger.LogInformation("Wayland desktop portal opened PipeWire remote successfully.");
    }

    return Result.Ok(new PortalSessionState(sessionHandle, startResult.Value.Streams, pipeWireFd, startResult.Value.ClipboardEnabled));
  }

  private async Task<(uint response, IDictionary<string, object> results)> WaitForResponse(
    string expectedRequestPath,
    Func<Task> trigger,
    CancellationToken cancellationToken = default)
  {
    var tcs = new TaskCompletionSource<(uint, IDictionary<string, object>)>(TaskCreationOptions.RunContinuationsAsynchronously);
    var requestProxy = Connection.CreateProxy<IRequest>(PortalBusName, new ObjectPath(expectedRequestPath));

    using var signalSubscription = await requestProxy.WatchResponseAsync(
      data =>
      {
        _logger.LogDebug("Received Response signal for {Path}: code={Code}", expectedRequestPath, data.response);
        tcs.TrySetResult((data.response, data.results));
      },
      ex =>
      {
        _logger.LogError(ex, "Error in Response signal handler for {Path}", expectedRequestPath);
        tcs.TrySetException(ex);
      });

    await trigger();

    using var defaultTimeout = new CancellationTokenSource(_userInteractionTimeout);
    using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, defaultTimeout.Token);
    var sw = Stopwatch.StartNew();
    try
    {
      return await tcs.Task.WaitAsync(combinedCts.Token);
    }
    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
    {
      throw new TimeoutException($"Timeout ({sw.Elapsed.TotalSeconds}s) waiting for portal response at {expectedRequestPath}");
    }
  }

  private sealed record PortalSessionState(
    string SessionHandle,
    List<PipeWireStreamInfo> Streams,
    SafeFileHandle? PipeWireFd,
    bool ClipboardEnabled);
}
