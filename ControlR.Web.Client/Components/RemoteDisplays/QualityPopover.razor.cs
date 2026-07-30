namespace ControlR.Web.Client.Components.RemoteDisplays;

public class QualityPopoverBase : DisposableComponent
{
  [Inject]
  public required ILogger<QualityPopoverBase> Logger { get; init; }
  [Inject]
  public required IRemoteControlState RemoteControlState { get; init; }
  [Inject]
  public required IViewerRemoteControlStream RemoteControlStream { get; init; }
  [Inject]
  public required ISnackbar Snackbar { get; init; }

  protected async Task HandleAutoQualityLowerThresholdChanged(double value)
  {
    var oldValue = RemoteControlState.AutoQualityLowerThresholdMbps;
    RemoteControlState.AutoQualityLowerThresholdMbps = value;
    if (!await SendCaptureSettings())
    {
      RemoteControlState.AutoQualityLowerThresholdMbps = oldValue;
    }
  }

  protected async Task HandleAutoQualityMaximumChanged(int value)
  {
    var oldValue = RemoteControlState.AutoQualityMaximum;
    RemoteControlState.AutoQualityMaximum = value;
    if (!await SendCaptureSettings())
    {
      RemoteControlState.AutoQualityMaximum = oldValue;
    }
  }

  protected async Task HandleAutoQualityMinimumChanged(int value)
  {
    var oldValue = RemoteControlState.AutoQualityMinimum;
    RemoteControlState.AutoQualityMinimum = value;
    if (!await SendCaptureSettings())
    {
      RemoteControlState.AutoQualityMinimum = oldValue;
    }
  }

  protected async Task HandleAutoQualityToggled(bool value)
  {
    var oldValue = RemoteControlState.IsAutoQualityEnabled;
    RemoteControlState.IsAutoQualityEnabled = value;
    if (!await SendCaptureSettings())
    {
      RemoteControlState.IsAutoQualityEnabled = oldValue;
    }
  }

  protected async Task HandleAutoQualityUpperThresholdChanged(double value)
  {
    var oldValue = RemoteControlState.AutoQualityUpperThresholdMbps;
    RemoteControlState.AutoQualityUpperThresholdMbps = value;
    if (!await SendCaptureSettings())
    {
      RemoteControlState.AutoQualityUpperThresholdMbps = oldValue;
    }
  }

  protected async Task HandleEncodingFormatChanged(ImageFormat value)
  {
    var oldValue = RemoteControlState.EncodingFormat;
    RemoteControlState.EncodingFormat = value;
    if (!await SendCaptureSettings())
    {
      RemoteControlState.EncodingFormat = oldValue;
    }
  }

  protected async Task HandleManualQualityChanged(int value)
  {
    var oldValue = RemoteControlState.ManualQuality;
    RemoteControlState.ManualQuality = value;
    if (!await SendCaptureSettings())
    {
      RemoteControlState.ManualQuality = oldValue;
    }
  }

  protected async Task HandleMaxBandwidthChanged(double value)
  {
    var oldValue = RemoteControlState.MaxBandwidthMbps;
    RemoteControlState.MaxBandwidthMbps = value;
    if (!await SendCaptureSettings())
    {
      RemoteControlState.MaxBandwidthMbps = oldValue;
    }
  }

  protected async Task HandleMaxBandwidthToggled(bool value)
  {
    var oldValue = RemoteControlState.IsMaxBandwidthEnabled;
    RemoteControlState.IsMaxBandwidthEnabled = value;
    if (!await SendCaptureSettings())
    {
      RemoteControlState.IsMaxBandwidthEnabled = oldValue;
    }
  }

  protected override async Task OnAfterRenderAsync(bool firstRender)
  {
    if (firstRender)
    {
      Disposables.Add(RemoteControlState.OnStateChanged(() => InvokeAsync(StateHasChanged)));
    }

    await base.OnAfterRenderAsync(firstRender);
  }

  private async Task<bool> SendCaptureSettings()
  {
    try
    {
      if (RemoteControlStream.State != System.Net.WebSockets.WebSocketState.Open)
      {
        return false;
      }

      using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
      var dto = new UpdateCaptureSettingsDto(
        RemoteControlState.CaptureCursor,
        RemoteControlState.EnableDirectX,
        RemoteControlState.IsAutoQualityEnabled,
        RemoteControlState.ManualQuality,
        RemoteControlState.AutoQualityLowerThresholdMbps,
        RemoteControlState.AutoQualityMaximum,
        RemoteControlState.AutoQualityMinimum,
        RemoteControlState.AutoQualityUpperThresholdMbps,
        RemoteControlState.IsMaxBandwidthEnabled,
        RemoteControlState.MaxBandwidthMbps,
        RemoteControlState.EncodingFormat);

      await RemoteControlStream.SendCaptureSettings(dto, cts.Token);
      return true;
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error while sending capture settings.");
      Snackbar.Add("An error occurred while updating capture settings", Severity.Error);
      return false;
    }
  }
}
