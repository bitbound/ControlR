using ControlR.Libraries.Shared.Constants;
using ControlR.Libraries.Shared.Primitives;
using Microsoft.Extensions.Logging;
using Microsoft.Win32.SafeHandles;
using Tmds.DBus;

namespace ControlR.Libraries.NativeInterop.Unix.Linux.XdgPortal;

public class XdgDesktopPortal(ILogger logger) : IDisposable
{
  private const string PortalBusName = "org.freedesktop.portal.Desktop";
  private const string PortalObjectPath = "/org/freedesktop/portal/desktop";

  private readonly ILogger _logger = logger;

  private Connection? _connection;
  private bool _disposed;


  private Connection Connection => _connection
    ?? throw new InvalidOperationException("DBus connection is not established.");


  public static async Task<XdgDesktopPortal> CreateAsync(ILogger logger, CancellationToken ct = default)
  {
    var portal = new XdgDesktopPortal(logger);
    await portal.ConnectAsync(ct).ConfigureAwait(false);
    return portal;
  }


  public async Task<Result<string>> CreateRemoteDesktopSessionAsync(CancellationToken ct = default)
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

  public async Task<Result<string>> CreateScreenCastSessionAsync(CancellationToken ct = default)
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
        "org.freedesktop.portal.ScreenCast",
        "CreateSession",
        requestToken,
        options,
        TimeSpan.FromSeconds(180),
        ct).ConfigureAwait(false);

      if (response != 0)
      {
        return Result.Fail<string>($"ScreenCast session creation failed with response code {response}");
      }

      if (results.TryGetValue("session_handle", out var sessionHandle) && sessionHandle is string handle)
      {
        _logger.LogInformation("Created ScreenCast session: {SessionHandle}", handle);
        return Result.Ok(handle);
      }

      return Result.Fail<string>("Session creation succeeded but no session handle returned");
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error creating ScreenCast session");
      return Result.Fail<string>($"Exception creating session: {ex.Message}");
    }
  }

  public void Dispose()
  {
    if (_disposed)
    {
      return;
    }

    try
    {
      _connection?.Dispose();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error disposing DBus connection");
    }

    _disposed = true;
  }

  public async Task<bool> IsRemoteDesktopAvailableAsync()
  {
    try
    {
      var proxy = Connection.CreateProxy<IRemoteDesktop>(PortalBusName, PortalObjectPath);
      await proxy.GetAsync<uint>("version").ConfigureAwait(false);
      return true;
    }
    catch
    {
      return false;
    }
  }

  public async Task<bool> IsScreenCastAvailableAsync()
  {
    try
    {
      var proxy = Connection.CreateProxy<IScreenCast>(PortalBusName, PortalObjectPath);
      await proxy.GetAsync<uint>("version").ConfigureAwait(false);
      return true;
    }
    catch
    {
      return false;
    }
  }

  public async Task<Result> NotifyKeyboardKeycodeAsync(string sessionHandle, int keycode, bool pressed)
  {
    try
    {
      var proxy = Connection.CreateProxy<IRemoteDesktop>(PortalBusName, PortalObjectPath);
      await proxy.NotifyKeyboardKeycodeAsync(new ObjectPath(sessionHandle), new Dictionary<string, object>(), keycode, pressed ? 1u : 0u).ConfigureAwait(false);
      return Result.Ok();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error sending keyboard keycode");
      return Result.Fail($"Exception sending keyboard keycode: {ex.Message}");
    }
  }

  public async Task<Result> NotifyPointerAxisAsync(string sessionHandle, double dx, double dy, bool finish = true)
  {
    try
    {
      var options = new Dictionary<string, object> { ["finish"] = finish };
      var proxy = Connection.CreateProxy<IRemoteDesktop>(PortalBusName, PortalObjectPath);
      await proxy.NotifyPointerAxisAsync(new ObjectPath(sessionHandle), options, dx, dy).ConfigureAwait(false);
      return Result.Ok();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error sending pointer axis");
      return Result.Fail($"Exception sending pointer axis: {ex.Message}");
    }
  }

  public async Task<Result> NotifyPointerAxisDiscreteAsync(string sessionHandle, uint axis, int steps)
  {
    try
    {
      var proxy = Connection.CreateProxy<IRemoteDesktop>(PortalBusName, PortalObjectPath);
      await proxy.NotifyPointerAxisDiscreteAsync(new ObjectPath(sessionHandle), new Dictionary<string, object>(), axis, steps).ConfigureAwait(false);
      return Result.Ok();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error sending discrete pointer axis");
      return Result.Fail($"Exception sending discrete pointer axis: {ex.Message}");
    }
  }

  public async Task<Result> NotifyPointerButtonAsync(string sessionHandle, int button, bool pressed)
  {
    try
    {
      var proxy = Connection.CreateProxy<IRemoteDesktop>(PortalBusName, PortalObjectPath);
      await proxy.NotifyPointerButtonAsync(new ObjectPath(sessionHandle), new Dictionary<string, object>(), button, pressed ? 1u : 0u).ConfigureAwait(false);
      return Result.Ok();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error sending pointer button");
      return Result.Fail($"Exception sending pointer button: {ex.Message}");
    }
  }

  public async Task<Result> NotifyPointerMotionAbsoluteAsync(string sessionHandle, uint stream, double x, double y)
  {
    try
    {
      var proxy = Connection.CreateProxy<IRemoteDesktop>(PortalBusName, PortalObjectPath);
      await proxy.NotifyPointerMotionAbsoluteAsync(
        new ObjectPath(sessionHandle),
        new Dictionary<string, object>(),
        stream,
        x,
        y).ConfigureAwait(false);
      return Result.Ok();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error sending absolute pointer motion");
      return Result.Fail($"Exception sending absolute pointer motion: {ex.Message}");
    }
  }

  public async Task<Result> NotifyPointerMotionAsync(string sessionHandle, double dx, double dy)
  {
    try
    {
      var proxy = Connection.CreateProxy<IRemoteDesktop>(PortalBusName, PortalObjectPath);
      await proxy.NotifyPointerMotionAsync(new ObjectPath(sessionHandle), new Dictionary<string, object>(), dx, dy).ConfigureAwait(false);
      return Result.Ok();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error sending pointer motion");
      return Result.Fail($"Exception sending pointer motion: {ex.Message}");
    }
  }

  public async Task<Result<SafeFileHandle>> OpenPipeWireRemoteAsync(string sessionHandle)
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

  public async Task<Result> SelectRemoteDesktopDevicesAsync(
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

  public async Task<Result> SelectScreenCastSourcesAsync(
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

  public async Task<Result<RemoteDesktopStartResult>> StartRemoteDesktopAsync(
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
        _logger.LogDebug("Parsing streams from RemoteDesktop.Start. Type: {Type}", streamsObj?.GetType().FullName ?? "null");

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
                  NodeId = nodeId,
                  Properties = new Dictionary<string, object>(props)
                });
                _logger.LogInformation("RemoteDesktop stream: NodeId={NodeId}", nodeId);
              }
              else if (entry is System.Collections.IDictionary dict && dict.Contains(0) && dict.Contains(1))
              {
                var nodeId = Convert.ToUInt32(dict[0]);
                var props = dict[1] as IDictionary<string, object> ?? new Dictionary<string, object>();
                streams.Add(new PipeWireStreamInfo
                {
                  NodeId = nodeId,
                  Properties = new Dictionary<string, object>(props)
                });
                _logger.LogInformation("RemoteDesktop stream: NodeId={NodeId}", nodeId);
              }
              else
              {
                var entryType = entry?.GetType();
                var fields = entryType?.GetFields() ?? Array.Empty<System.Reflection.FieldInfo>();
                if (fields.Length >= 2)
                {
                  var nodeId = Convert.ToUInt32(fields[0].GetValue(entry));
                  var propsObj = fields[1].GetValue(entry);
                  var props = propsObj as IDictionary<string, object> ?? new Dictionary<string, object>();
                  streams.Add(new PipeWireStreamInfo
                  {
                    NodeId = nodeId,
                    Properties = new Dictionary<string, object>(props)
                  });
                  _logger.LogInformation("RemoteDesktop stream: NodeId={NodeId}", nodeId);
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
      else
      {
        _logger.LogWarning("No 'streams' key found in RemoteDesktop.Start results. Available keys: {Keys}", string.Join(", ", results.Keys));
      }

      return Result.Ok(new RemoteDesktopStartResult { Streams = streams, RestoreToken = restoreToken });
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error starting RemoteDesktop");
      return Result.Fail<RemoteDesktopStartResult>($"Exception starting RemoteDesktop: {ex.Message}");
    }
  }

  public async Task<Result<ScreenCastStartResult>> StartScreenCastAsync(
    string sessionHandle,
    string parentWindow = "",
    CancellationToken ct = default)
  {
    try
    {
      _logger.LogInformation("Starting ScreenCast session: {Session}", sessionHandle);
      var requestToken = $"controlr_request_{Guid.NewGuid():N}";

      var options = new Dictionary<string, object>
      {
        ["handle_token"] = requestToken
      };

      var proxy = Connection.CreateProxy<IScreenCast>(PortalBusName, PortalObjectPath);
      var requestPath = await proxy.StartAsync(new ObjectPath(sessionHandle), parentWindow, options).ConfigureAwait(false);

      var (response, results) = await WaitForResponseAsync(requestPath.ToString(), TimeSpan.FromSeconds(180), ct).ConfigureAwait(false);

      _logger.LogInformation("Received ScreenCast Start response: {Response}", response);

      if (response != 0)
      {
        return Result.Fail<ScreenCastStartResult>($"Start failed with response code {response}. User may have denied permission.");
      }

      var streams = new List<PipeWireStreamInfo>();
      string? restoreToken = null;

      if (results.TryGetValue("restore_token", out var tokenObj) && tokenObj is string token)
      {
        restoreToken = token;
      }
      if (results.TryGetValue("streams", out var streamsObj))
      {
        _logger.LogDebug("Parsing streams from response. Type: {Type}", streamsObj?.GetType().FullName ?? "null");

        if (streamsObj is System.Collections.IEnumerable enumerable)
        {
          foreach (var entry in enumerable)
          {
            try
            {
              _logger.LogDebug("Stream entry type: {Type}", entry?.GetType().FullName ?? "null");

              // Try different possible formats
              if (entry is ValueTuple<uint, IDictionary<string, object>> streamTuple)
              {
                var nodeId = streamTuple.Item1;
                var props = streamTuple.Item2;
                streams.Add(new PipeWireStreamInfo
                {
                  NodeId = nodeId,
                  Properties = new Dictionary<string, object>(props)
                });
                _logger.LogInformation("ScreenCast stream: NodeId={NodeId}", nodeId);
              }
              else if (entry is System.Collections.IDictionary dict)
              {
                // DBus might return it as a dictionary with index keys
                if (dict.Contains(0) && dict.Contains(1))
                {
                  var nodeId = Convert.ToUInt32(dict[0]);
                  var props = dict[1] as IDictionary<string, object> ?? new Dictionary<string, object>();
                  streams.Add(new PipeWireStreamInfo
                  {
                    NodeId = nodeId,
                    Properties = new Dictionary<string, object>(props)
                  });
                  _logger.LogInformation("ScreenCast stream: NodeId={NodeId}", nodeId);
                }
              }
              else if (entry != null)
              {
                // Try to use reflection to get the fields
                var entryType = entry.GetType();
                var fields = entryType.GetFields();
                if (fields.Length >= 2)
                {
                  var nodeId = Convert.ToUInt32(fields[0].GetValue(entry));
                  var propsObj = fields[1].GetValue(entry);
                  var props = propsObj as IDictionary<string, object> ?? new Dictionary<string, object>();
                  streams.Add(new PipeWireStreamInfo
                  {
                    NodeId = nodeId,
                    Properties = new Dictionary<string, object>(props)
                  });
                  _logger.LogInformation("ScreenCast stream: NodeId={NodeId}", nodeId);
                }
              }
            }
            catch (Exception parseEx)
            {
              _logger.LogWarning(parseEx, "Failed parsing a stream entry");
            }
          }
        }
      }
      else
      {
        _logger.LogWarning("No 'streams' key found in results. Available keys: {Keys}", string.Join(", ", results.Keys));
      }

      if (streams.Count == 0)
      {
        return Result.Fail<ScreenCastStartResult>("Start succeeded but no streams returned");
      }

      _logger.LogInformation("Successfully started ScreenCast with {Count} streams", streams.Count);
      return Result.Ok(new ScreenCastStartResult { Streams = streams, RestoreToken = restoreToken });
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error starting ScreenCast");
      return Result.Fail<ScreenCastStartResult>($"Exception starting ScreenCast: {ex.Message}");
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
        // Use the system bus in Wayland login screen context
        _connection = new Connection(Address.System);
        await _connection.ConnectAsync().ConfigureAwait(false);
        _logger.LogDebug("Connected to DBus system bus (Wayland login screen)");
        return;
      }

      _connection = new Connection(Address.Session);
      await _connection.ConnectAsync().ConfigureAwait(false);
      _logger.LogDebug("Connected to DBus session bus");

      // Note: We'll subscribe to signals on a per-request basis in CallPortalMethodAsync
      // The high-level Tmds.DBus doesn't have a global WatchSignalAsync method
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to connect to DBus session bus");
      throw;
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
      // Subscribe to the Response signal for this specific request
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
