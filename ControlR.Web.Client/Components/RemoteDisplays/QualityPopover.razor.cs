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
    RemoteControlState.AutoQualityLowerThresholdMbps = value;
    await SendCaptureSettings();
  }

  protected async Task HandleAutoQualityMaximumChanged(int value)
  {
    RemoteControlState.AutoQualityMaximum = value;
    await SendCaptureSettings();
  }

  protected async Task HandleAutoQualityMinimumChanged(int value)
  {
    RemoteControlState.AutoQualityMinimum = value;
    await SendCaptureSettings();
  }

  protected async Task HandleAutoQualityToggled(bool value)
  {
    RemoteControlState.IsAutoQualityEnabled = value;
    await SendCaptureSettings();
  }

  protected async Task HandleAutoQualityUpperThresholdChanged(double value)
  {
    RemoteControlState.AutoQualityUpperThresholdMbps = value;
    await SendCaptureSettings();
  }

  protected async Task HandleManualQualityChanged(int value)
  {
    RemoteControlState.ManualQuality = value;
    await SendCaptureSettings();
  }

  protected async Task HandleMaxBandwidthChanged(double value)
  {
    RemoteControlState.MaxBandwidthMbps = value;
    await SendCaptureSettings();
  }

  protected async Task HandleMaxBandwidthToggled(bool value)
  {
    RemoteControlState.IsMaxBandwidthEnabled = value;
    await SendCaptureSettings();
  }

  protected override async Task OnAfterRenderAsync(bool firstRender)
  {
    if (firstRender)
    {
      Disposables.Add(RemoteControlState.OnStateChanged(() => InvokeAsync(StateHasChanged)));
    }

    await base.OnAfterRenderAsync(firstRender);
  }

  private async Task SendCaptureSettings()
  {
    try
    {
      if (RemoteControlStream.State != System.Net.WebSockets.WebSocketState.Open)
      {
        return;
      }

      using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
      var dto = new UpdateCaptureSettingsDto(
        RemoteControlState.CaptureCursor,
        RemoteControlState.IsAutoQualityEnabled,
        RemoteControlState.ManualQuality,
        RemoteControlState.AutoQualityLowerThresholdMbps,
        RemoteControlState.AutoQualityMaximum,
        RemoteControlState.AutoQualityMinimum,
        RemoteControlState.AutoQualityUpperThresholdMbps,
        RemoteControlState.IsMaxBandwidthEnabled,
        RemoteControlState.MaxBandwidthMbps);

      await RemoteControlStream.SendCaptureSettings(dto, cts.Token);
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error while sending capture settings.");
      Snackbar.Add("An error occurred while updating capture settings", Severity.Error);
    }
  }
}
