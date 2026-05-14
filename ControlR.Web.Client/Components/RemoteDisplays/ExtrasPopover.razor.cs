namespace ControlR.Web.Client.Components.RemoteDisplays;

public partial class ExtrasPopover : DisposableComponent
{
  [Inject]
  public required IDeviceState DeviceState { get; init; }

  [Inject]
  public required ILogger<ExtrasPopover> Logger { get; init; }

  [Inject]
  public required IRemoteControlState RemoteControlState { get; init; }

  [Inject]
  public required IViewerRemoteControlStream RemoteControlStream { get; init; }

  [Inject]
  public required ISnackbar Snackbar { get; init; }

  private bool IsWindows
  {
    get
    {
      if (!DeviceState.IsDeviceLoaded)
      {
        return false;
      }
      return DeviceState.CurrentDevice.Platform == SystemPlatform.Windows;
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

  private async Task HandleDirectXChanged(bool value)
  {
    RemoteControlState.EnableDirectX = value;

    try
    {
      if (RemoteControlStream.State != System.Net.WebSockets.WebSocketState.Open)
      {
        return;
      }

      using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
      await RemoteControlStream.SendCaptureSettings(
        new UpdateCaptureSettingsDto(
          RemoteControlState.CaptureCursor,
          RemoteControlState.EnableDirectX,
          RemoteControlState.IsAutoQualityEnabled,
          RemoteControlState.ManualQuality,
          RemoteControlState.AutoQualityLowerThresholdMbps,
          RemoteControlState.AutoQualityMaximum,
          RemoteControlState.AutoQualityMinimum,
          RemoteControlState.AutoQualityUpperThresholdMbps,
          RemoteControlState.IsMaxBandwidthEnabled,
          RemoteControlState.MaxBandwidthMbps),
        cts.Token);
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error while sending DirectX setting.");
      Snackbar.Add("An error occurred while updating DirectX setting", Severity.Error);
    }
  }

  private void HandleMetricsToggled(bool value)
  {
    RemoteControlState.IsMetricsEnabled = value;
  }
}