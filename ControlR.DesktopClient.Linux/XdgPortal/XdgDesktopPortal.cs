using ControlR.DesktopClient.Common.Options;
using ControlR.Libraries.DevicesCommon.Services;
using ControlR.Libraries.Shared.Constants;
using ControlR.Libraries.Shared.Primitives;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Win32.SafeHandles;
using Tmds.DBus;

namespace ControlR.DesktopClient.Linux.XdgPortal;

public interface IXdgDesktopPortal : IDisposable
{
  Task<(SafeFileHandle Fd, string SessionHandle)?> GetPipeWireConnection();
  Task<string?> GetRemoteDesktopSessionHandle();
  Task<List<PipeWireStreamInfo>> GetScreenCastStreams();
  Task Initialize();
  Task NotifyKeyboardKeycodeAsync(string sessionHandle, int keycode, bool pressed);
  Task NotifyPointerAxisAsync(string sessionHandle, double dx, double dy, bool finish = true);
  Task NotifyPointerAxisDiscreteAsync(string sessionHandle, uint axis, int steps);
  Task NotifyPointerButtonAsync(string sessionHandle, int button, bool pressed);
  Task NotifyPointerMotionAbsoluteAsync(string sessionHandle, uint stream, double x, double y);
  Task NotifyPointerMotionAsync(string sessionHandle, double dx, double dy);
}

public sealed class XdgDesktopPortal(
  IFileSystem fileSystem,
  IOptionsMonitor<DesktopClientOptions> options,
  ILogger<XdgDesktopPortal> logger) : IXdgDesktopPortal, IDisposable
{
  private const string PortalBusName = "org.freedesktop.portal.Desktop";
  private const string PortalObjectPath = "/org/freedesktop/portal/desktop";

  private readonly IFileSystem _fileSystem = fileSystem;
  private readonly SemaphoreSlim _initLock = new(1, 1);
  private readonly ILogger<XdgDesktopPortal> _logger = logger;
  private readonly IOptionsMonitor<DesktopClientOptions> _options = options;

  private Connection? _connection;
  private bool _disposed;
  private bool _initialized;
  private SafeFileHandle? _pipewireFd;
  private string? _sessionHandle;
  private List<PipeWireStreamInfo>? _streams;

  private Connection Connection => _connection
    ?? throw new InvalidOperationException("DBus connection is not established.");

  public void Dispose()
  {
    if (_disposed) return;
    _pipewireFd?.Dispose();
    _connection?.Dispose();
    _initLock?.Dispose();
    _pipewireFd = null;
    _connection = null;
    _disposed = true;
  }

  public async Task<(SafeFileHandle Fd, string SessionHandle)?> GetPipeWireConnection()
  {
    await EnsureInitializedAsync();
    return _pipewireFd != null && _sessionHandle != null
    ? (_pipewireFd, _sessionHandle)
    : throw new InvalidOperationException("PipeWire connection is not initialized.");
  }

  public async Task<string?> GetRemoteDesktopSessionHandle()
  {
    await EnsureInitializedAsync();
    return _sessionHandle;
  }

  public async Task<List<PipeWireStreamInfo>> GetScreenCastStreams()
  {
    await EnsureInitializedAsync();
    return _streams ?? throw new InvalidOperationException("ScreenCast streams are not initialized.");
  }

  public async Task Initialize()
  {
    await EnsureInitializedAsync();
  }

  public async Task NotifyKeyboardKeycodeAsync(string sessionHandle, int keycode, bool pressed)
  {
    try
    {
      await EnsureInitializedAsync();
      var proxy = Connection.CreateProxy<IRemoteDesktop>(PortalBusName, PortalObjectPath);
      await proxy.NotifyKeyboardKeycodeAsync(new ObjectPath(sessionHandle), new Dictionary<string, object>(), keycode, pressed ? 1u : 0u).ConfigureAwait(false);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error sending keyboard keycode");
    }
  }

  public async Task NotifyPointerAxisAsync(string sessionHandle, double dx, double dy, bool finish = true)
  {
    try
    {
      await EnsureInitializedAsync();
      var options = new Dictionary<string, object> { ["finish"] = finish };
      var proxy = Connection.CreateProxy<IRemoteDesktop>(PortalBusName, PortalObjectPath);
      await proxy.NotifyPointerAxisAsync(new ObjectPath(sessionHandle), options, dx, dy).ConfigureAwait(false);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error sending pointer axis");
    }
  }

  public async Task NotifyPointerAxisDiscreteAsync(string sessionHandle, uint axis, int steps)
  {
    try
    {
      await EnsureInitializedAsync();
      var proxy = Connection.CreateProxy<IRemoteDesktop>(PortalBusName, PortalObjectPath);
      await proxy.NotifyPointerAxisDiscreteAsync(new ObjectPath(sessionHandle), new Dictionary<string, object>(), axis, steps).ConfigureAwait(false);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error sending discrete pointer axis");
    }
  }

  public async Task NotifyPointerButtonAsync(string sessionHandle, int button, bool pressed)
  {
    try
    {
      await EnsureInitializedAsync();
      var proxy = Connection.CreateProxy<IRemoteDesktop>(PortalBusName, PortalObjectPath);
      await proxy.NotifyPointerButtonAsync(new ObjectPath(sessionHandle), new Dictionary<string, object>(), button, pressed ? 1u : 0u).ConfigureAwait(false);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error sending pointer button");
    }
  }

  public async Task NotifyPointerMotionAbsoluteAsync(string sessionHandle, uint stream, double x, double y)
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
        y).ConfigureAwait(false);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error sending absolute pointer motion");
    }
  }

  public async Task NotifyPointerMotionAsync(string sessionHandle, double dx, double dy)
  {
    try
    {
      await EnsureInitializedAsync();
      var proxy = Connection.CreateProxy<IRemoteDesktop>(PortalBusName, PortalObjectPath);
      await proxy.NotifyPointerMotionAsync(
        new ObjectPath(sessionHandle), 
        new Dictionary<string, object>(), 
        dx, 
        dy).ConfigureAwait(false);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error sending pointer motion");
    }
  }

  private async Task<(uint, IDictionary<string, object>)> CallPortalMethodAsync(
    string interfaceName,
    string methodName,
    string requestToken,
    IDictionary<string, object> options,
    TimeSpan timeout,
    CancellationToken ct = default)
  {
    ObjectPath requestPath;
    if (interfaceName.Contains("ScreenCast"))
    {
      var proxy = Connection.CreateProxy<IScreenCast>(PortalBusName, PortalObjectPath);
      requestPath = methodName switch
      {
        "CreateSession" => await proxy.CreateSessionAsync(options).ConfigureAwait(false),
        _ => throw new ArgumentException($"Unknown ScreenCast method: {methodName}")
      };
    }
    else if (interfaceName.Contains("RemoteDesktop"))
    {
      var proxy = Connection.CreateProxy<IRemoteDesktop>(PortalBusName, PortalObjectPath);
      requestPath = methodName switch
      {
        "CreateSession" => await proxy.CreateSessionAsync(options).ConfigureAwait(false),
        _ => throw new ArgumentException($"Unknown RemoteDesktop method: {methodName}")
      };
    }
    else
    {
      throw new ArgumentException($"Unknown interface: {interfaceName}");
    }

    var pathStr = requestPath.ToString();
    _logger.LogDebug("Portal {Method} returned request path: {Path}", methodName, pathStr);

    return await WaitForResponseAsync(pathStr, timeout, ct).ConfigureAwait(false);
  }

  private async Task ConnectAsync(CancellationToken ct = default)
  {
    try
    {
      if (Environment.GetEnvironmentVariable(AppConstants.WaylandLoginScreenVariable) is { } waylandLoginScreen &&
          bool.TryParse(waylandLoginScreen, out var isLoginScreen) &&
          isLoginScreen)
      {
        _connection = new Connection(Address.System);
        await _connection.ConnectAsync().ConfigureAwait(false);
        _logger.LogDebug("Connected to DBus system bus (Wayland login screen)");
        return;
      }

      _connection = new Connection(Address.Session);
      await _connection.ConnectAsync().ConfigureAwait(false);
      _logger.LogDebug("Connected to DBus session bus");
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to connect to DBus session bus");
      throw;
    }
  }

  private async Task<Result<string>> CreateRemoteDesktopSessionAsync(CancellationToken ct = default)
  {
    try
    {
      var sessionToken = $"controlr_session_{Guid.NewGuid():N}";
      var requestToken = $"controlr_request_{Guid.NewGuid():N}";

      var options = new Dictionary<string, object>
      {
        ["handle_token"] = requestToken,
        ["session_handle_token"] = sessionToken
      };

      var (response, results) = await CallPortalMethodAsync(
        "org.freedesktop.portal.RemoteDesktop",
        "CreateSession",
        requestToken,
        options,
        TimeSpan.FromSeconds(180),
        ct).ConfigureAwait(false);

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

  private async Task EnsureInitializedAsync(bool force = false)
  {
    if (_initialized) return;

    await _initLock.WaitAsync();
    try
    {
      if (_initialized) return;

      await ConnectAsync();

      var sessionResult = await CreateRemoteDesktopSessionAsync();
      if (!sessionResult.IsSuccess || sessionResult.Value is null)
      {
        _logger.LogError("Failed to create RemoteDesktop session: {Error}", sessionResult.Reason);
        return;
      }

      _sessionHandle = sessionResult.Value;
      _logger.LogInformation("Created RemoteDesktop session: {Session}", _sessionHandle);

      var remoteDesktopOptions = new Dictionary<string, object> { ["persist_mode"] = 2u };
      if (!force)
      {
        var restoreToken = LoadRestoreToken();
        if (!string.IsNullOrEmpty(restoreToken))
        {
          remoteDesktopOptions["restore_token"] = restoreToken;
          _logger.LogInformation("Using saved restore token");
        }
      }

      var selectResult = await SelectRemoteDesktopDevicesAsync(
        _sessionHandle,
        deviceTypes: 3,
        additionalOptions: remoteDesktopOptions);

      if (!selectResult.IsSuccess)
      {
        _logger.LogError("Failed to select RemoteDesktop devices: {Error}", selectResult.Reason);
        return;
      }

      var selectSourcesResult = await SelectScreenCastSourcesAsync(
        _sessionHandle,
        sourceTypes: 1,
        multipleSources: true,
        cursorMode: 2);

      if (!selectSourcesResult.IsSuccess)
      {
        _logger.LogError("Failed to select ScreenCast sources: {Error}", selectSourcesResult.Reason);
        return;
      }

      var startResult = await StartRemoteDesktopAsync(_sessionHandle);
      if (!startResult.IsSuccess || startResult.Value is null)
      {
        _logger.LogError("Failed to start RemoteDesktop: {Error}", startResult.Reason);
        return;
      }

      _streams = startResult.Value.Streams;
      _logger.LogInformation("Combined session started with {Count} stream(s)", _streams?.Count ?? 0);

      if (!string.IsNullOrEmpty(startResult.Value.RestoreToken))
      {
        SaveRestoreToken(startResult.Value.RestoreToken);
        _logger.LogInformation("Saved restore token");
      }

      var fdResult = await OpenPipeWireRemoteAsync(_sessionHandle);
      if (fdResult.IsSuccess && fdResult.Value != null)
      {
        _pipewireFd = fdResult.Value;
      }

      _initialized = true;
    }
    finally
    {
      _initLock.Release();
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

  private async Task<Result<SafeFileHandle>> OpenPipeWireRemoteAsync(string sessionHandle)
  {
    try
    {
      var proxy = Connection.CreateProxy<IScreenCast>(PortalBusName, PortalObjectPath);
      var fd = await proxy.OpenPipeWireRemoteAsync(new ObjectPath(sessionHandle), new Dictionary<string, object>()).ConfigureAwait(false);

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
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to save restore token");
    }
  }

  private async Task<Result> SelectRemoteDesktopDevicesAsync(
    string sessionHandle,
    uint deviceTypes = 3,
    Dictionary<string, object>? additionalOptions = null,
    CancellationToken ct = default)
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

      var proxy = Connection.CreateProxy<IRemoteDesktop>(PortalBusName, PortalObjectPath);
      var requestPath = await proxy.SelectDevicesAsync(new ObjectPath(sessionHandle), options).ConfigureAwait(false);

      var (response, _) = await WaitForResponseAsync(requestPath.ToString(), TimeSpan.FromSeconds(180), ct).ConfigureAwait(false);

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

  private async Task<Result> SelectScreenCastSourcesAsync(
    string sessionHandle,
    uint sourceTypes = 1,
    bool multipleSources = true,
    uint cursorMode = 4,
    Dictionary<string, object>? additionalOptions = null,
    CancellationToken ct = default)
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

      var proxy = Connection.CreateProxy<IScreenCast>(PortalBusName, PortalObjectPath);
      var requestPath = await proxy.SelectSourcesAsync(new ObjectPath(sessionHandle), options).ConfigureAwait(false);

      var (response, _) = await WaitForResponseAsync(requestPath.ToString(), TimeSpan.FromSeconds(180), ct).ConfigureAwait(false);

      if (response != 0)
      {
        return Result.Fail($"SelectSources failed with response code {response}");
      }

      _logger.LogInformation("Successfully selected ScreenCast sources");
      return Result.Ok();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error selecting ScreenCast sources");
      return Result.Fail($"Exception selecting sources: {ex.Message}");
    }
  }

  private async Task<Result<RemoteDesktopStartResult>> StartRemoteDesktopAsync(
    string sessionHandle,
    string parentWindow = "",
    CancellationToken ct = default)
  {
    try
    {
      var requestToken = $"controlr_request_{Guid.NewGuid():N}";

      var options = new Dictionary<string, object>
      {
        ["handle_token"] = requestToken
      };

      var proxy = Connection.CreateProxy<IRemoteDesktop>(PortalBusName, PortalObjectPath);
      var requestPath = await proxy.StartAsync(new ObjectPath(sessionHandle), parentWindow, options).ConfigureAwait(false);

      var (response, results) = await WaitForResponseAsync(requestPath.ToString(), TimeSpan.FromSeconds(180), ct).ConfigureAwait(false);

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

      return Result.Ok(new RemoteDesktopStartResult { Streams = streams, RestoreToken = restoreToken });
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error starting RemoteDesktop");
      return Result.Fail<RemoteDesktopStartResult>($"Exception starting RemoteDesktop: {ex.Message}");
    }
  }

  private async Task<(uint, IDictionary<string, object>)> WaitForResponseAsync(
    string requestPath,
    TimeSpan timeout,
    CancellationToken ct = default)
  {
    var tcs = new TaskCompletionSource<(uint, IDictionary<string, object>)>(TaskCreationOptions.RunContinuationsAsynchronously);
    var requestProxy = Connection.CreateProxy<IRequest>(PortalBusName, new ObjectPath(requestPath));

    IDisposable? signalSubscription = null;
    try
    {
      signalSubscription = await requestProxy.WatchResponseAsync(
        data =>
        {
          _logger.LogDebug("Received Response signal for {Path}: code={Code}", requestPath, data.response);
          tcs.TrySetResult((data.response, data.results));
        },
        ex =>
        {
          _logger.LogError(ex, "Error in Response signal handler for {Path}", requestPath);
          tcs.TrySetException(ex);
        }).ConfigureAwait(false);

      using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
      combinedCts.CancelAfter(timeout);

      try
      {
        return await tcs.Task.WaitAsync(combinedCts.Token).ConfigureAwait(false);
      }
      catch (OperationCanceledException) when (!ct.IsCancellationRequested)
      {
        throw new TimeoutException($"Timeout ({timeout.TotalSeconds}s) waiting for portal response at {requestPath}");
      }
    }
    finally
    {
      signalSubscription?.Dispose();
    }
  }
}
