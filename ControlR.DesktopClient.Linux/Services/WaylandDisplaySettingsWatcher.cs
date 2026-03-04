using Bitbound.SimpleMessenger;
using ControlR.DesktopClient.Common.Messages;
using ControlR.DesktopClient.Linux.XdgPortal;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ControlR.DesktopClient.Linux.Services;

internal sealed class WaylandDisplaySettingsWatcher(
  TimeProvider timeProvider,
  IXdgDesktopPortal portalService,
  IDisplayManagerWayland displayManager,
  IMessenger messenger,
  ILogger<WaylandDisplaySettingsWatcher> logger) : BackgroundService
{
  private static readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(1);

  private readonly IDisplayManagerWayland _displayManager = displayManager;
  private readonly ILogger<WaylandDisplaySettingsWatcher> _logger = logger;
  private readonly IMessenger _messenger = messenger;
  private readonly IXdgDesktopPortal _portalService = portalService;
  private readonly TimeProvider _timeProvider = timeProvider;

  private string? _lastSnapshot;

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    while (!stoppingToken.IsCancellationRequested)
    {
      try
      {
        await Task.Delay(_pollInterval, _timeProvider, stoppingToken);

        // Avoid initializing the portal (and triggering permission prompts) until capture has started.
        if (!_displayManager.HasAnyCaptureSizes)
        {
          continue;
        }

        var streams = await _portalService.GetScreenCastStreams();

        var snapshot = string.Join(
          "|",
          streams
            .OrderBy(x => x.StreamIndex)
            .Select(s =>
            {
              var deviceName = s.StreamIndex.ToString();

              var position = s.Properties.TryGetValue("position", out var posObj)
                ? NormalizeTuple2(posObj)
                : "";

              var size = s.Properties.TryGetValue("size", out var sizeObj)
                ? NormalizeTuple2(sizeObj)
                : "";

              var captureSize = _displayManager.TryGetCaptureSize(deviceName, out var cap)
                ? $"{cap.Width}x{cap.Height}"
                : "";

              return $"{s.StreamIndex}:{s.NodeId}:{position}:{size}:{captureSize}";
            }));

        if (_lastSnapshot is null)
        {
          _lastSnapshot = snapshot;
          _logger.LogInformation("Initial Wayland display snapshot captured. Sending display refresh.");
          await _messenger.Send(new DisplaySettingsChangedMessage());
          continue;
        }

        if (!string.Equals(_lastSnapshot, snapshot, StringComparison.Ordinal))
        {
          _lastSnapshot = snapshot;
          _logger.LogInformation("Display settings changed: {Snapshot}", snapshot);
          await _messenger.Send(new DisplaySettingsChangedMessage());
        }
      }
      catch (OperationCanceledException)
      {
        break;
      }
      catch (Exception ex)
      {
        _logger.LogDebug(ex, "Error while watching Wayland display settings.");
      }
    }
  }

  private static string NormalizeTuple2(object? value)
  {
    if (!WaylandTupleParser.TryParseTuple2(value, out var x, out var y))
    {
      return string.Empty;
    }

    return $"{x},{y}";
  }
}
