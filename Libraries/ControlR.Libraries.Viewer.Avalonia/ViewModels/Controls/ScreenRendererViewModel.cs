using Avalonia;
using ControlR.Libraries.Shared.Dtos.RemoteControlDtos;
using ControlR.Libraries.Viewer.Common;
using ControlR.Libraries.Viewer.Common.State;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;

namespace ControlR.Libraries.Viewer.Avalonia.ViewModels.Controls;

public interface IScreenRendererViewModel : IDisposable
{
  Channel<CaptureFrame> FrameChannel { get; }
  ILogger Logger { get; }
}

#pragma warning disable
public class ScreenRendererViewModel : IScreenRendererViewModel
{
  private const int MaxBufferedFrames = 2;
  private readonly ILogger<ScreenRenderer> _logger;
  private readonly IDisposable _messageHandlerRegistration;
  private readonly IRemoteControlState _remoteControlState;
  private readonly IMessenger _messenger;
  private readonly IViewerRemoteControlStream _viewerStream;
  private double _selectedDisplayWidth;
  private double _selectedDisplayHeight;

  public ScreenRendererViewModel(
    IViewerRemoteControlStream viewerStream,
    IRemoteControlState remoteControlState,
    IMessenger messenger,
    ILogger<ScreenRenderer> logger)
  {
    _viewerStream = viewerStream;
    _remoteControlState = remoteControlState;
    _messenger = messenger;
    _logger = logger;

    var channelOptions = new BoundedChannelOptions(MaxBufferedFrames)
    {
      FullMode = BoundedChannelFullMode.Wait,
      SingleReader = true,
      SingleWriter = true,
    };

    FrameChannel = Channel.CreateBounded<CaptureFrame>(channelOptions);
    _messageHandlerRegistration = _viewerStream.RegisterMessageHandler(this, HandleRemoteControlDtoReceived);
  }

  public Channel<CaptureFrame> FrameChannel { get; }

  public ILogger Logger => _logger;

  public void Dispose()
  {
    _messageHandlerRegistration.Dispose();
  }
  private async Task HandleDisplayDataReceived(DisplayDataDto dto)
  {
    _remoteControlState.DisplayData = dto.Displays;

    if (_remoteControlState.DisplayData.Length == 0)
    {
      //Snackbar.Add("No displays received", Severity.Error);
      //await OnDisconnectRequested.InvokeAsync();
      return;
    }

    var selectedDisplay = _remoteControlState.DisplayData
      .FirstOrDefault(x => x.IsPrimary)
      ?? _remoteControlState.DisplayData[0];

    _remoteControlState.SelectedDisplay = selectedDisplay;

    _selectedDisplayWidth = selectedDisplay.Width;
    _selectedDisplayHeight = selectedDisplay.Height;
  }
  private async Task DrawRegion(ScreenRegionDto dto)
  {
    try
    {
      var frameSize = new PixelSize((int)_selectedDisplayWidth, (int)_selectedDisplayHeight);
      using var imageStream = new MemoryStream(dto.EncodedImage);
      var bitmap = SKBitmap.Decode(imageStream);
      var captureFrame = new CaptureFrame(dto.X, dto.Y, bitmap);
      await FrameChannel.Writer.WriteAsync(captureFrame);
    }
    catch (Exception ex)
    {
      _logger?.LogError(ex, "Error while drawing render frame.");
    }
  }

  private async Task HandleRemoteControlDtoReceived(DtoWrapper message)
  {
    try
    {
      switch (message.DtoType)
      {
        case DtoType.DisplayData:
          {
            var dto = message.GetPayload<DisplayDataDto>();
            await HandleDisplayDataReceived(dto);
            break;
          }
        case DtoType.ScreenRegion:
          {
            var dto = message.GetPayload<ScreenRegionDto>();
            await DrawRegion(dto);
            break;
          }
        case DtoType.ClipboardText:
          {
            //var dto = message.GetPayload<ClipboardTextDto>();
            //await HandleClipboardTextReceived(dto);
            break;
          }
        case DtoType.CursorChanged:
          {
            //var dto = message.GetPayload<CursorChangedDto>();
            //await HandleCursorChanged(dto);
            break;
          }
        case DtoType.WindowsSessionEnding:
          {
            //Snackbar.Add("Remote Windows session ending", Severity.Warning);
            //await OnDisconnectRequested.InvokeAsync();
            break;
          }
        case DtoType.WindowsSessionSwitched:
          {
            //Snackbar.Add("Remote Windows session switched", Severity.Info);
            break;
          }
        case DtoType.CaptureMetricsChanged:
          {
            //var dto = message.GetPayload<CaptureMetricsDto>();
            //_latestCaptureMetrics = dto;
            //_currentLatency = RemoteControlStream.CurrentLatency;
            //_mbpsIn = RemoteControlStream.GetMbpsIn();
            //_mbpsOut = RemoteControlStream.GetMbpsOut();
            //await InvokeAsync(StateHasChanged);
            break;
          }
        default:
          Logger.LogWarning("Received unsupported DTO type: {DtoType}", message.DtoType);
          //Snackbar.Add($"Unsupported DTO type: {message.DtoType}", Severity.Warning);
          break;
      }
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error while handling remote control DTO. Type: {DtoType}", message.DtoType);
    }
  }
}
