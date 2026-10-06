using System.Diagnostics;
using ControlR.DesktopClient.Common.Options;
using ControlR.DesktopClient.Linux.XdgPortal;
using ControlR.Libraries.Shared.Services.FileSystem;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ControlR.DesktopClient.Linux.Services;

public interface IWaylandPermissionProvider
{
  void DeleteRestoreToken();
  bool HasRestoreToken();
  Task<bool> IsRemoteControlPermissionGranted();
  Task<bool> RequestRemoteControlPermission(bool bypassRestoreToken = false, CancellationToken cancellationToken = default);
}

internal class WaylandPermissionProvider(
  TimeProvider timeProvider,
  IFileSystem fileSystem,
  IXdgDesktopPortalFactory xdgFactory,
  IOptionsMonitor<DesktopClientOptions> options,
  ILogger<WaylandPermissionProvider> logger) : IWaylandPermissionProvider
{
  private static readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(5);

  // Serializes probes across every host in the process. The restore token is single-use and
  // the portal rotates it on success, so overlapping probes can invalidate each other. Static
  // because each remote control session runs its own host with its own provider instance,
  // while the token file is shared process-wide.
  private static readonly SemaphoreSlim _probeLock = new(1, 1);

  private readonly IFileSystem _fileSystem = fileSystem;
  private readonly ILogger<WaylandPermissionProvider> _logger = logger;
  private readonly IOptionsMonitor<DesktopClientOptions> _options = options;
  private readonly TimeProvider _timeProvider = timeProvider;
  private readonly IXdgDesktopPortalFactory _xdgFactory = xdgFactory;

  private bool _cachedProbeResult;
  private string? _cachedTokenValue;
  private DateTimeOffset _lastCacheProbeTime;

  public void DeleteRestoreToken()
  {
    try
    {
      var tokenPath = PathConstants.GetWaylandRemoteDesktopRestoreTokenPath(_options.CurrentValue.InstanceId);
      if (_fileSystem.FileExists(tokenPath))
      {
        _fileSystem.DeleteFile(tokenPath);
        _logger.LogInformation("Deleted stale restore token");
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error deleting restore token.");
    }
    finally
    {
      InvalidateCache();
    }
  }

  public bool HasRestoreToken()
  {
    var tokenPath = PathConstants.GetWaylandRemoteDesktopRestoreTokenPath(_options.CurrentValue.InstanceId);
    return _fileSystem.FileExists(tokenPath);
  }

  public async Task<bool> IsRemoteControlPermissionGranted()
  {
    await _probeLock.WaitAsync();
    try
    {
      var timer = Stopwatch.StartNew();
      var restoreToken = LoadRestoreToken();
      if (string.IsNullOrEmpty(restoreToken))
      {
        _logger.LogInformation("Wayland permission check found no restore token.");
        InvalidateCache();
        return false;
      }

      if (IsCacheValid(restoreToken))
      {
        _logger.LogInformation("Wayland permission check used cached restore token result: Granted={Granted}", _cachedProbeResult);
        return _cachedProbeResult;
      }

      _logger.LogInformation("Wayland permission probe starting.");
      using var xdgPortal = _xdgFactory.CreateNew();
      var isGranted = await xdgPortal.ProbeRestoreToken(restoreToken);
      timer.Stop();

      _logger.LogInformation(
        "Wayland permission probe completed in {ElapsedMilliseconds}ms. Granted={Granted}",
        timer.ElapsedMilliseconds,
        isGranted);

      // A failed probe is not proof that the token is no longer valid. The portal ignores
      // an unusable token and prompts instead of reporting an error, so the file stays on
      // disk and the next granted session overwrites it.
      UpdateCache(restoreToken, isGranted);
      return isGranted;
    }
    catch (OperationCanceledException)
    {
      _logger.LogWarning("Wayland permission probe timed out or was canceled.");
      return false;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error checking RemoteDesktop permission");
      return false;
    }
    finally
    {
      _probeLock.Release();
    }
  }

  public async Task<bool> RequestRemoteControlPermission(bool bypassRestoreToken = false, CancellationToken cancellationToken = default)
  {
    try
    {
      _logger.LogInformation(
        "Starting Wayland remote control permission request. BypassRestoreToken={BypassRestoreToken}",
        bypassRestoreToken);

      using var xdgPortal = _xdgFactory.CreateNew();
      var result = await xdgPortal.RequestRemoteDesktopPermission(bypassRestoreToken, cancellationToken);

      if (result)
      {
        _logger.LogInformation("RemoteDesktop permission granted via XDG portal");
      }
      else
      {
        _logger.LogWarning("Wayland remote control permission request did not complete successfully.");
      }

      return result;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error requesting RemoteDesktop permission");
      return false;
    }
  }

  private void InvalidateCache()
  {
    _cachedTokenValue = null;
    _cachedProbeResult = false;
    _lastCacheProbeTime = default;
  }

  private bool IsCacheValid(string currentToken)
  {
    return string.Equals(_cachedTokenValue, currentToken, StringComparison.Ordinal)
      && _lastCacheProbeTime != default
      && _timeProvider.GetUtcNow() - _lastCacheProbeTime < _cacheDuration;
  }

  private string? LoadRestoreToken()
  {
    try
    {
      var tokenPath = PathConstants.GetWaylandRemoteDesktopRestoreTokenPath(_options.CurrentValue.InstanceId);
      if (_fileSystem.FileExists(tokenPath))
      {
        _logger.LogDebug("Loading Wayland restore token from {TokenPath}", tokenPath);
        return _fileSystem.ReadAllText(tokenPath).Trim();
      }
    }
    catch (Exception ex)
    {
      _logger.LogWarning(ex, "Failed to load restore token");
    }
    return null;
  }

  private void UpdateCache(string tokenValue, bool result)
  {
    _cachedTokenValue = tokenValue;
    _cachedProbeResult = result;
    _lastCacheProbeTime = _timeProvider.GetUtcNow();
  }
}
