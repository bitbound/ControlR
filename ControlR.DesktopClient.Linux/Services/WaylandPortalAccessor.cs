using System.Diagnostics.CodeAnalysis;
using ControlR.DesktopClient.Common.Options;
using ControlR.Libraries.DevicesCommon.Services;
using ControlR.Libraries.NativeInterop.Unix.Linux.XdgPortal;
using ControlR.Libraries.Shared.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Win32.SafeHandles;

namespace ControlR.DesktopClient.Linux.Services;

public interface IWaylandPortalAccessor
{
  Task<(SafeFileHandle Fd, string SessionHandle)?> GetPipeWireConnection();
  Task<(XdgDesktopPortal Portal, string SessionHandle)?> GetRemoteDesktopSession();
  Task<List<PipeWireStreamInfo>> GetScreenCastStreams();
  Task Initialize();
  Task Uninitialize();
}

internal class WaylandPortalAccessor(
  IFileSystem fileSystem,
  IOptionsMonitor<DesktopClientOptions> options,
  ILogger<WaylandPortalAccessor> logger) : IWaylandPortalAccessor, IDisposable
{
  private readonly IFileSystem _fileSystem = fileSystem;
  private readonly SemaphoreSlim _initLock = new(1, 1);
  private readonly ILogger<WaylandPortalAccessor> _logger = logger;
  private readonly IOptionsMonitor<DesktopClientOptions> _options = options;


  private bool _disposed;
  private bool _initialized;
  private SafeFileHandle? _pipewireFd;
  private XdgDesktopPortal? _portal;
  private string? _sessionHandle;
  private List<PipeWireStreamInfo>? _streams;

  private XdgDesktopPortal Portal => _portal ?? throw new InvalidOperationException("XDG Desktop Portal is not initialized.");

  public void Dispose()
  {
    if (_disposed) return;
    _pipewireFd?.Dispose();
    _portal?.Dispose();
    _initLock?.Dispose();
    _disposed = true;
  }

  public async Task<(SafeFileHandle Fd, string SessionHandle)?> GetPipeWireConnection()
  {
    await EnsureInitializedAsync();
    return _pipewireFd != null && _sessionHandle != null
    ? (_pipewireFd, _sessionHandle)
    : throw new InvalidOperationException("PipeWire connection is not initialized.");
  }

  public async Task<(XdgDesktopPortal Portal, string SessionHandle)?> GetRemoteDesktopSession()
  {
    await EnsureInitializedAsync();
    return _portal != null && _sessionHandle != null
      ? (_portal, _sessionHandle)
      : throw new InvalidOperationException("RemoteDesktop session is not initialized.");
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
  public async Task Uninitialize()
  {
    try
    {
      using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
      using var locker = await _initLock.AcquireLockAsync(cts.Token);
      
      if (!_initialized) return;

      _pipewireFd?.Dispose();
      _pipewireFd = null;

      _portal?.Dispose();
      _portal = null;

      _sessionHandle = null;
      _streams = null;
      _initialized = false;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error during WaylandPortalAccessor uninitialization");
    }
  }

  private async Task EnsureInitializedAsync()
  {
    if (_initialized) return;

    await _initLock.WaitAsync();
    try
    {
      if (_initialized) return;

      await EnsurePortalConnectedAsync();

      if (!await Portal.IsRemoteDesktopAvailableAsync())
      {
        _logger.LogError("XDG Desktop Portal RemoteDesktop is not available");
        return;
      }

      var sessionResult = await Portal.CreateRemoteDesktopSessionAsync();
      if (!sessionResult.IsSuccess || sessionResult.Value is null)
      {
        _logger.LogError("Failed to create RemoteDesktop session: {Error}", sessionResult.Reason);
        return;
      }

      _sessionHandle = sessionResult.Value;
      _logger.LogInformation("Created RemoteDesktop session: {Session}", _sessionHandle);

      var restoreToken = LoadRestoreToken();
      var remoteDesktopOptions = new Dictionary<string, object> { ["persist_mode"] = 2u };
      if (!string.IsNullOrEmpty(restoreToken))
      {
        remoteDesktopOptions["restore_token"] = restoreToken;
        _logger.LogInformation("Using saved restore token");
      }

      var selectResult = await Portal.SelectRemoteDesktopDevicesAsync(
        _sessionHandle,
        deviceTypes: 3,
        additionalOptions: remoteDesktopOptions);

      if (!selectResult.IsSuccess)
      {
        _logger.LogError("Failed to select RemoteDesktop devices: {Error}", selectResult.Reason);
        return;
      }

      var selectSourcesResult = await Portal.SelectScreenCastSourcesAsync(
        _sessionHandle,
        sourceTypes: 1,
        multipleSources: true,
        cursorMode: 2);

      if (!selectSourcesResult.IsSuccess)
      {
        _logger.LogError("Failed to select ScreenCast sources: {Error}", selectSourcesResult.Reason);
        return;
      }

      var startResult = await Portal.StartRemoteDesktopAsync(_sessionHandle);
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

      var fdResult = await Portal.OpenPipeWireRemoteAsync(_sessionHandle);
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

  private async Task EnsurePortalConnectedAsync()
  {
    _portal ??= await XdgDesktopPortal.CreateAsync(_logger);
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
}
