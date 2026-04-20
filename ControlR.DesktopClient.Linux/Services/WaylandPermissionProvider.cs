using System.Diagnostics;
using ControlR.DesktopClient.Common.Options;
using ControlR.DesktopClient.Linux.XdgPortal;
using ControlR.Libraries.Shared.Services.FileSystem;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;

namespace ControlR.DesktopClient.Linux.Services;

public interface IWaylandPermissionProvider
{
  void DeleteRestoreToken();
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

      InvalidateCache();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error deleting restore token");
    }
  }

  public async Task<bool> IsRemoteControlPermissionGranted()
  {
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
      var isValid = await xdgPortal.ProbeRestoreToken(restoreToken);
      timer.Stop();

      _logger.LogInformation(
        "Wayland permission probe completed in {ElapsedMilliseconds}ms. Granted={Granted}",
        timer.ElapsedMilliseconds,
        isValid);

      UpdateCache(restoreToken, isValid);

      if (!isValid)
      {
        _logger.LogWarning("Restore token probe failed, deleting stale token");
        DeleteRestoreToken();
      }

      return isValid;
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
