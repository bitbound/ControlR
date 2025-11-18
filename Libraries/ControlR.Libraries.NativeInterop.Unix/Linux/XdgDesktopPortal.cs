using ControlR.Libraries.Shared.Primitives;
using Microsoft.Extensions.Logging;
using Microsoft.Win32.SafeHandles;
using System.Collections.Concurrent;
using Tmds.DBus.Protocol;

namespace ControlR.Libraries.NativeInterop.Unix.Linux;

public class XdgDesktopPortal : IDisposable
{
  private const string PortalBusName = "org.freedesktop.portal.Desktop";
  private const string PortalObjectPath = "/org/freedesktop/portal/desktop";
  private const string RemoteDesktopInterface = "org.freedesktop.portal.RemoteDesktop";
  private const string RequestInterface = "org.freedesktop.portal.Request";
  private const string ScreenCastInterface = "org.freedesktop.portal.ScreenCast";

  private Connection? _connection;
  private string? _uniqueNameTransformed;
  private readonly ILogger _logger;
  private readonly ConcurrentDictionary<string, TaskCompletionSource<(uint, Dictionary<string, VariantValue>)>> _pendingRequests = new();
  private readonly SemaphoreSlim _signalLock = new(1, 1);
  private IDisposable? _globalSignalSubscription;
  private bool _disposed;

  public XdgDesktopPortal(ILogger logger)
  {
    _logger = logger;
  }

  public static async Task<XdgDesktopPortal> CreateAsync(ILogger logger, CancellationToken ct = default)
  {
    var portal = new XdgDesktopPortal(logger);
    await portal.ConnectAsync(ct).ConfigureAwait(false);
    return portal;
  }

  private async Task ConnectAsync(CancellationToken ct = default)
  {
    try
    {
      var sessionAddress = Address.Session;
      if (string.IsNullOrEmpty(sessionAddress))
      {
        throw new InvalidOperationException("DBus session bus address not found (DBUS_SESSION_BUS_ADDRESS).");
      }

      _connection = new Connection(sessionAddress);
      await _connection.ConnectAsync().ConfigureAwait(false);

      var unique = _connection.UniqueName;
      if (string.IsNullOrEmpty(unique) || unique.Length < 2 || unique[0] != ':')
      {
        throw new InvalidOperationException($"DBus connection obtained invalid unique name: '{unique}'");
      }

      _uniqueNameTransformed = unique[1..].Replace('.', '_');
      _logger.LogDebug("Connected to DBus session bus as {Unique}", unique);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to connect to DBus session bus");
      throw;
    }
  }

  private async Task SetupGlobalSignalHandlerAsync()
  {
    await _signalLock.WaitAsync().ConfigureAwait(false);
    try
    {
      if (_globalSignalSubscription is not null)
      {
        return;
      }

      var rule = new MatchRule
      {
        Type = MessageType.Signal,
        Interface = RequestInterface,
        Member = "Response"
      };

      _globalSignalSubscription = await _connection!.AddMatchAsync<(string, uint, Dictionary<string, VariantValue>)>(
        rule,
        (Message m, object? s) =>
        {
          var r = m.GetBodyReader();
          var path = m.Path.ToString();
          var code = r.ReadUInt32();
          var dictEnd = r.ReadDictionaryStart();
          var map = new Dictionary<string, VariantValue>();
          while (r.HasNext(dictEnd))
          {
            var key = r.ReadString();
            var vv = r.ReadVariantValue();
            map[key] = vv;
          }
          return (path, code, map);
        },
        (Exception? ex, (string, uint, Dictionary<string, VariantValue>) data, object? _, object? __) =>
        {
          if (ex is not null)
          {
            _logger.LogError(ex, "Signal handler error");
            return;
          }

          var (path, code, results) = data;
          if (_pendingRequests.TryRemove(path, out var tcs))
          {
            tcs.TrySetResult((code, results));
          }
        },
        (ObserverFlags)3,
        readerState: null,
        handlerState: null,
        emitOnCapturedContext: false).ConfigureAwait(false);

      _logger.LogDebug("Global signal handler set up");
    }
    finally
    {
      _signalLock.Release();
    }
  }

  private string BuildRequestPath(string handleToken)
  {
    if (string.IsNullOrEmpty(_uniqueNameTransformed))
    {
      throw new InvalidOperationException("DBus connection not initialized.");
    }
    return $"{PortalObjectPath}/request/{_uniqueNameTransformed}/{handleToken}";
  }

  private async Task<(uint, Dictionary<string, VariantValue>)> CallPortalWithResponseAsync(
    string iface,
    string member,
    string handleToken,
    Action<MessageWriter> writeArgs,
    TimeSpan timeout,
    CancellationToken ct = default)
  {
    var expectedPath = BuildRequestPath(handleToken);
    var tcs = new TaskCompletionSource<(uint, Dictionary<string, VariantValue>)>(TaskCreationOptions.RunContinuationsAsynchronously);

    MessageBuffer message;
    using (var writer = _connection!.GetMessageWriter())
    {
      string signature = member switch
      {
        "CreateSession" => "a{sv}",
        "SelectSources" => "oa{sv}",
        "SelectDevices" => "oa{sv}",
        "Start" => "osa{sv}",
        _ => throw new ArgumentException($"Unknown portal method: {member}")
      };

      writer.WriteMethodCallHeader(
        destination: PortalBusName,
        path: PortalObjectPath,
        @interface: iface,
        signature: signature,
        member: member);
      writeArgs(writer);
      message = writer.CreateMessage();
    }

    var returnedHandle = await _connection.CallMethodAsync(message, (Message m, object? s) =>
    {
      var r = m.GetBodyReader();
      return r.ReadObjectPath().ToString();
    }).ConfigureAwait(false);

    _logger.LogDebug("Portal {Member} returned handle: {Handle}", member, returnedHandle);

    if (!string.IsNullOrEmpty(returnedHandle))
    {
      expectedPath = returnedHandle;
    }

    _pendingRequests[expectedPath] = tcs;

    IDisposable? subscription = null;
    try
    {
      var rule = new MatchRule
      {
        Type = MessageType.Signal,
        Interface = RequestInterface,
        Member = "Response",
        Path = expectedPath
      };

      subscription = await _connection.AddMatchAsync<(uint, Dictionary<string, VariantValue>)>(
        rule,
        (Message m, object? s) =>
        {
          var r = m.GetBodyReader();
          var code = r.ReadUInt32();
          var dictEnd = r.ReadDictionaryStart();
          var map = new Dictionary<string, VariantValue>();
          while (r.HasNext(dictEnd))
          {
            var key = r.ReadString();
            var vv = r.ReadVariantValue();
            map[key] = vv;
          }
          return (code, map);
        },
        (Exception? ex, (uint, Dictionary<string, VariantValue>) data, object? _, object? __) =>
        {
          if (ex is not null)
          {
            _logger.LogError(ex, "Signal handler error for {Path}", expectedPath);
            if (_pendingRequests.TryRemove(expectedPath, out var tcs2))
            {
              tcs2.TrySetException(ex);
            }
            return;
          }

          if (_pendingRequests.TryRemove(expectedPath, out var tcs3))
          {
            tcs3.TrySetResult(data);
          }
        },
        (ObserverFlags)3,
        readerState: null,
        handlerState: null,
        emitOnCapturedContext: false).ConfigureAwait(false);

      _logger.LogDebug("Subscribed to signals for {Path}", expectedPath);

      using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
      combinedCts.CancelAfter(timeout);

      try
      {
        return await tcs.Task.WaitAsync(combinedCts.Token).ConfigureAwait(false);
      }
      catch (OperationCanceledException) when (!ct.IsCancellationRequested)
      {
        throw new TimeoutException($"Timeout ({timeout.TotalSeconds}s) waiting for portal response at {expectedPath}");
      }
    }
    finally
    {
      subscription?.Dispose();
      _pendingRequests.TryRemove(expectedPath, out _);
    }
  }

  private static void WriteOptions(MessageWriter writer, IDictionary<string, object> dict)
  {
    var dictStart = writer.WriteDictionaryStart();
    foreach (var kv in dict)
    {
      writer.WriteString(kv.Key);
      var vv = ConvertObjectToVariant(kv.Value);
      writer.WriteVariant(vv);
    }
    writer.WriteDictionaryEnd(dictStart);
  }

  private static VariantValue ConvertObjectToVariant(object value)
  {
    return value switch
    {
      null => VariantValue.String(""),
      string s => VariantValue.String(s),
      bool b => VariantValue.Bool(b),
      int i => VariantValue.Int32(i),
      uint ui => VariantValue.UInt32(ui),
      double d => VariantValue.Double(d),
      ObjectPath op => VariantValue.ObjectPath(op),
      _ => VariantValue.String(value.ToString() ?? string.Empty)
    };
  }

  public async Task<Result<string>> CreateRemoteDesktopSessionAsync(CancellationToken ct = default)
  {
    try
    {
      var sessionToken = $"controlr_session_{Guid.NewGuid():N}";
      var requestToken = $"controlr_request_{Guid.NewGuid():N}";
      var (response, results) = await CallPortalWithResponseAsync(
        RemoteDesktopInterface,
        "CreateSession",
        requestToken,
        writer =>
        {
          var options = new Dictionary<string, object>
          {
            ["handle_token"] = requestToken,
            ["session_handle_token"] = sessionToken
          };
          WriteOptions(writer, options);
        },
        TimeSpan.FromSeconds(180),
        ct).ConfigureAwait(false);

      if (response != 0)
      {
        return Result.Fail<string>($"RemoteDesktop session creation failed with response code {response}");
      }

      if (results.TryGetValue("session_handle", out var sessionVv))
      {
        var handle = sessionVv.GetObjectPathAsString();
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
      var (response, results) = await CallPortalWithResponseAsync(
        ScreenCastInterface,
        "CreateSession",
        requestToken,
        writer =>
        {
          var options = new Dictionary<string, object>
          {
            ["handle_token"] = requestToken,
            ["session_handle_token"] = sessionToken
          };
          WriteOptions(writer, options);
        },
        TimeSpan.FromSeconds(180),
        ct).ConfigureAwait(false);

      if (response != 0)
      {
        return Result.Fail<string>($"ScreenCast session creation failed with response code {response}");
      }

      if (results.TryGetValue("session_handle", out var sessionVv))
      {
        var handle = sessionVv.GetObjectPathAsString();
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
      _globalSignalSubscription?.Dispose();
      _connection?.Dispose();
      _signalLock?.Dispose();
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
      return await IsInterfaceAvailableAsync(RemoteDesktopInterface).ConfigureAwait(false);
    }
    catch (Exception ex)
    {
      _logger.LogDebug(ex, "RemoteDesktop portal not available");
      return false;
    }
  }

  public async Task<bool> IsScreenCastAvailableAsync()
  {
    try
    {
      return await IsInterfaceAvailableAsync(ScreenCastInterface).ConfigureAwait(false);
    }
    catch (Exception ex)
    {
      _logger.LogDebug(ex, "ScreenCast portal not available");
      return false;
    }
  }

  public async Task<Result> NotifyKeyboardKeycodeAsync(string sessionHandle, int keycode, bool pressed)
  {
    try
    {
      await CallNotifyAsync(
        "NotifyKeyboardKeycode",
        "oa{sv}iu",
        writer =>
        {
          writer.WriteObjectPath(new ObjectPath(sessionHandle));
          WriteOptions(writer, new Dictionary<string, object>());
          writer.WriteInt32(keycode);
          writer.WriteUInt32(pressed ? 1u : 0u);
        }).ConfigureAwait(false);
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
      await CallNotifyAsync(
        "NotifyPointerAxis",
        "oa{sv}dd",
        writer =>
        {
          writer.WriteObjectPath(new ObjectPath(sessionHandle));
          WriteOptions(writer, new Dictionary<string, object> { ["finish"] = finish });
          writer.WriteDouble(dx);
          writer.WriteDouble(dy);
        }).ConfigureAwait(false);
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
      await CallNotifyAsync(
        "NotifyPointerAxisDiscrete",
        "oa{sv}ui",
        writer =>
        {
          writer.WriteObjectPath(new ObjectPath(sessionHandle));
          WriteOptions(writer, new Dictionary<string, object>());
          writer.WriteUInt32(axis);
          writer.WriteInt32(steps);
        }).ConfigureAwait(false);
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
      await CallNotifyAsync(
        "NotifyPointerButton",
        "oa{sv}iu",
        writer =>
        {
          writer.WriteObjectPath(new ObjectPath(sessionHandle));
          WriteOptions(writer, new Dictionary<string, object>());
          writer.WriteInt32(button);
          writer.WriteUInt32(pressed ? 1u : 0u);
        }).ConfigureAwait(false);
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
      await CallNotifyAsync(
        "NotifyPointerMotionAbsolute",
        "oa{sv}udd",
        writer =>
        {
          writer.WriteObjectPath(new ObjectPath(sessionHandle));
          WriteOptions(writer, new Dictionary<string, object>());
          writer.WriteUInt32(stream);
          writer.WriteDouble(x);
          writer.WriteDouble(y);
        }).ConfigureAwait(false);
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
      await CallNotifyAsync(
        "NotifyPointerMotion",
        "oa{sv}dd",
        writer =>
        {
          writer.WriteObjectPath(new ObjectPath(sessionHandle));
          WriteOptions(writer, new Dictionary<string, object>());
          writer.WriteDouble(dx);
          writer.WriteDouble(dy);
        }).ConfigureAwait(false);
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
      MessageBuffer message;
      using (var writer = _connection!.GetMessageWriter())
      {
        writer.WriteMethodCallHeader(
          destination: PortalBusName,
          path: PortalObjectPath,
          @interface: ScreenCastInterface,
          signature: "oa{sv}",
          member: "OpenPipeWireRemote");
        writer.WriteObjectPath(new ObjectPath(sessionHandle));
        WriteOptions(writer, new Dictionary<string, object>());
        message = writer.CreateMessage();
      }

      var handle = await _connection.CallMethodAsync(message, (Message m, object? s) =>
      {
        var r = m.GetBodyReader();
        var safe = r.ReadHandle<SafeFileHandle>();
        if (safe is null)
        {
          throw new InvalidOperationException("PipeWire FD missing from reply");
        }
        return safe;
      }).ConfigureAwait(false);
      
      _logger.LogInformation("Opened PipeWire remote with FD");
      return Result.Ok(handle);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error opening PipeWire remote");
      return Result.Fail<SafeFileHandle>($"Exception opening PipeWire remote: {ex.Message}");
    }
  }

  public async Task<Result> SelectRemoteDesktopDevicesAsync(string sessionHandle, uint deviceTypes = 3, CancellationToken ct = default)
  {
    try
    {
      var requestToken = $"controlr_request_{Guid.NewGuid():N}";
      var (response, _) = await CallPortalWithResponseAsync(
        RemoteDesktopInterface,
        "SelectDevices",
        requestToken,
        writer =>
        {
          writer.WriteObjectPath(new ObjectPath(sessionHandle));
          var options = new Dictionary<string, object>
          {
            ["handle_token"] = requestToken,
            ["types"] = deviceTypes
          };
          WriteOptions(writer, options);
        },
        TimeSpan.FromSeconds(180),
        ct).ConfigureAwait(false);

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

  public async Task<Result> SelectScreenCastSourcesAsync(string sessionHandle, uint sourceTypes = 1, bool multipleSources = false, uint cursorMode = 2, CancellationToken ct = default)
  {
    try
    {
      var requestToken = $"controlr_request_{Guid.NewGuid():N}";
      var (response, _) = await CallPortalWithResponseAsync(
        ScreenCastInterface,
        "SelectSources",
        requestToken,
        writer =>
        {
          writer.WriteObjectPath(new ObjectPath(sessionHandle));
          var options = new Dictionary<string, object>
          {
            ["handle_token"] = requestToken,
            ["types"] = sourceTypes,
            ["multiple"] = multipleSources,
            ["cursor_mode"] = cursorMode
          };
          WriteOptions(writer, options);
        },
        TimeSpan.FromSeconds(180),
        ct).ConfigureAwait(false);

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

  public async Task<Result<List<PipeWireStreamInfo>>> StartRemoteDesktopAsync(string sessionHandle, string parentWindow = "", CancellationToken ct = default)
  {
    try
    {
      var requestToken = $"controlr_request_{Guid.NewGuid():N}";
      var (response, results) = await CallPortalWithResponseAsync(
        RemoteDesktopInterface,
        "Start",
        requestToken,
        writer =>
        {
          writer.WriteObjectPath(new ObjectPath(sessionHandle));
          writer.WriteString(parentWindow);
          var options = new Dictionary<string, object>
          {
            ["handle_token"] = requestToken
          };
          WriteOptions(writer, options);
        },
        TimeSpan.FromSeconds(180),
        ct).ConfigureAwait(false);

      if (response != 0)
      {
        return Result.Fail<List<PipeWireStreamInfo>>($"Start failed with response code {response}. User may have denied permission.");
      }

      var streams = new List<PipeWireStreamInfo>();
      if (results.TryGetValue("devices", out var devicesVv))
      {
        try
        {
          var devices = devicesVv.GetUInt32();
          _logger.LogInformation("RemoteDesktop granted devices: {Devices}", devices);
        }
        catch { }
      }

      return Result.Ok(streams);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error starting RemoteDesktop");
      return Result.Fail<List<PipeWireStreamInfo>>($"Exception starting RemoteDesktop: {ex.Message}");
    }
  }

  public async Task<Result<List<PipeWireStreamInfo>>> StartScreenCastAsync(string sessionHandle, string parentWindow = "", CancellationToken ct = default)
  {
    try
    {
      _logger.LogInformation("Starting ScreenCast session: {Session}", sessionHandle);
      var requestToken = $"controlr_request_{Guid.NewGuid():N}";
      
      var (response, results) = await CallPortalWithResponseAsync(
        ScreenCastInterface,
        "Start",
        requestToken,
        writer =>
        {
          writer.WriteObjectPath(new ObjectPath(sessionHandle));
          writer.WriteString(parentWindow);
          var options = new Dictionary<string, object>
          {
            ["handle_token"] = requestToken
          };
          WriteOptions(writer, options);
        },
        TimeSpan.FromSeconds(180),
        ct).ConfigureAwait(false);

      _logger.LogInformation("Received ScreenCast Start response: {Response}", response);

      if (response != 0)
      {
        return Result.Fail<List<PipeWireStreamInfo>>($"Start failed with response code {response}. User may have denied permission.");
      }

      var streams = new List<PipeWireStreamInfo>();
      if (results.TryGetValue("streams", out var streamsVv) && streamsVv.Type == VariantValueType.Array)
      {
        _logger.LogDebug("Parsing streams from response");
        int count = streamsVv.Count;
        for (int i = 0; i < count; i++)
        {
          try
          {
            var entry = streamsVv.GetItem(i);
            var nodeId = entry.GetItem(0).GetUInt32();
            var propsVv = entry.GetItem(1);
            var props = VariantDictToPlain(propsVv);
            streams.Add(new PipeWireStreamInfo
            {
              NodeId = nodeId,
              Properties = props
            });
            _logger.LogInformation("ScreenCast stream: NodeId={NodeId}", nodeId);
          }
          catch (Exception parseEx)
          {
            _logger.LogWarning(parseEx, "Failed parsing a stream entry");
          }
        }
      }

      if (streams.Count == 0)
      {
        return Result.Fail<List<PipeWireStreamInfo>>("Start succeeded but no streams returned");
      }

      _logger.LogInformation("Successfully started ScreenCast with {Count} streams", streams.Count);
      return Result.Ok(streams);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error starting ScreenCast");
      return Result.Fail<List<PipeWireStreamInfo>>($"Exception starting ScreenCast: {ex.Message}");
    }
  }

  private Task CallNotifyAsync(string member, string signature, Action<MessageWriter> writeArgs)
  {
    MessageBuffer message;
    using (var writer = _connection!.GetMessageWriter())
    {
      writer.WriteMethodCallHeader(
        destination: PortalBusName,
        path: PortalObjectPath,
        @interface: RemoteDesktopInterface,
        signature: signature,
        member: member);
      writeArgs(writer);
      message = writer.CreateMessage();
    }
    return _connection.CallMethodAsync(message);
  }

  private async Task<bool> IsInterfaceAvailableAsync(string iface)
  {
    try
    {
      MessageBuffer message;
      using (var writer = _connection!.GetMessageWriter())
      {
        writer.WriteMethodCallHeader(
          destination: PortalBusName,
          path: PortalObjectPath,
          @interface: "org.freedesktop.DBus.Properties",
          signature: "ss",
          member: "Get");
        writer.WriteString(iface);
        writer.WriteString("version");
        message = writer.CreateMessage();
      }

      return await _connection.CallMethodAsync(message, (Message m, object? s) =>
      {
        try
        {
          var r = m.GetBodyReader();
          var _ = r.ReadVariantValue();
          return true;
        }
        catch
        {
          return false;
        }
      }).ConfigureAwait(false);
    }
    catch
    {
      return false;
    }
  }

  private static Dictionary<string, object> VariantDictToPlain(VariantValue vv)
  {
    var result = new Dictionary<string, object>();
    var dict = vv.GetDictionary<string, VariantValue>();
    foreach (var kv in dict)
    {
      result[kv.Key] = VariantToObject(kv.Value);
    }
    return result;
  }

  private static object VariantToObject(VariantValue vv)
  {
    switch (vv.Type)
    {
      case VariantValueType.String:
        return vv.GetString();
      case VariantValueType.Bool:
        return vv.GetBool();
      case VariantValueType.Int32:
        return vv.GetInt32();
      case VariantValueType.UInt32:
        return vv.GetUInt32();
      case VariantValueType.Double:
        return vv.GetDouble();
      case VariantValueType.ObjectPath:
        return vv.GetObjectPathAsString();
      case VariantValueType.Array:
        {
          var list = new List<object>();
          for (int i = 0; i < vv.Count; i++)
          {
            list.Add(VariantToObject(vv.GetItem(i)));
          }
          return list.ToArray();
        }
      case VariantValueType.Dictionary:
        return VariantDictToPlain(vv);
      case VariantValueType.Struct:
        {
          var arr = new object[vv.Count];
          for (int i = 0; i < vv.Count; i++)
          {
            arr[i] = VariantToObject(vv.GetItem(i));
          }
          return arr;
        }
      default:
        return vv.ToString(false);
    }
  }
}

public class PipeWireStreamInfo
{
  public uint NodeId { get; set; }
  public Dictionary<string, object> Properties { get; set; } = new();
}
