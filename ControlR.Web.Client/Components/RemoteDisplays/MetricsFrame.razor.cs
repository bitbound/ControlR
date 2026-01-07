namespace ControlR.Web.Client.Components.RemoteDisplays;

public partial class MetricsFrame : IDisposable
{
  private IDisposable? _messageHandlerRegistration;

  [Inject]
  public required ILogger<MetricsFrame> Logger { get; init; }

  [Inject]
  public required IMetricsState MetricsState { get; init; }
  
  [Inject]
  public required IViewerRemoteControlStream RemoteControlStream { get; init; }
  
  public void Dispose()
  {
    _messageHandlerRegistration?.Dispose();
    GC.SuppressFinalize(this);
  }

  protected override void OnInitialized()
  {
    base.OnInitialized();
    _messageHandlerRegistration = RemoteControlStream.RegisterMessageHandler(this, HandleRemoteControlDtoReceived);
  }

  private async Task HandleRemoteControlDtoReceived(DtoWrapper wrapper)
  {
    try
    {
      if (wrapper.DtoType == DtoType.CaptureMetricsChanged)
      {
        var dto = wrapper.GetPayload<CaptureMetricsDto>();
        MetricsState.CurrentMetrics = dto;
        MetricsState.CurrentLatency = RemoteControlStream.CurrentLatency;
        MetricsState.MbpsIn = RemoteControlStream.GetMbpsIn();
        MetricsState.MbpsOut = RemoteControlStream.GetMbpsOut();
        await InvokeAsync(StateHasChanged);
      }
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error handling RemoteControlDtoReceived in MetricsFrame.");
    }
  }
}
