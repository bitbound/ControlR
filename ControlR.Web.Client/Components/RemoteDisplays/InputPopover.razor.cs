namespace ControlR.Web.Client.Components.RemoteDisplays;

public partial class InputPopover : DisposableComponent
{
  private bool _isBlockInputDisabled;

  [Inject]
  public required IClipboardManager ClipboardManager { get; init; }
  [Inject]
  public required IDeviceState DeviceState { get; init; }
  [Inject]
  public required ILogger<InputPopover> Logger { get; init; }
  [Inject]
  public required IRemoteControlState RemoteControlState { get; init; }
  [Inject]
  public required IViewerRemoteControlStream RemoteControlStream { get; init; }
  [Inject]
  public required ISnackbar Snackbar { get; init; }
  [Inject]
  public required IHubConnection<IViewerHub> ViewerHub { get; init; }

  protected override async Task OnAfterRenderAsync(bool firstRender)
  {
    if (firstRender)
    {
      Disposables.AddRange(
        RemoteControlState.OnStateChanged(() => InvokeAsync(StateHasChanged)),
        RemoteControlStream.RegisterMessageHandler(this, HandleDtoReceived));
    }
    await base.OnAfterRenderAsync(firstRender);
  }

  private async Task HandleBlockInputToggled(bool value)
  {
    try
    {
      if (RemoteControlStream.State != System.Net.WebSockets.WebSocketState.Open)
      {
        Snackbar.Add("No active remote session", Severity.Error);
        return;
      }

      _isBlockInputDisabled = true;
      StateHasChanged();
      Snackbar.Add("Sending block input toggle", Severity.Info);
      using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
      await RemoteControlStream.SendToggleBlockInput(value, cts.Token);
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error while toggling block input.");
      Snackbar.Add("An error occurred while toggling block input", Severity.Error);
    }
  }

  private async Task HandleDtoReceived(DtoWrapper wrapper)
  {
    try
    {
      switch (wrapper.DtoType)
      {
        case DtoType.BlockInputResult:
          var dto = wrapper.GetPayload<BlockInputResultDto>();
          RemoteControlState.IsBlockUserInputEnabled = dto.FinalState;
          _isBlockInputDisabled = false;
          await InvokeAsync(StateHasChanged);

          if (dto.IsSuccess)
          {
            Snackbar.Add($"Input blocking {(dto.FinalState ? "enabled" : "disabled")}", Severity.Success);
          }
          else
          {
            Snackbar.Add($"Failed to {(dto.FinalState ? "disable" : "enable")} input blocking", Severity.Error);
          }
          break;
        default:
          break;
      }
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error while handling received DTO of type {DtoType}.", wrapper.DtoType);
      Snackbar.Add($"An error occurred while handling DTO type {wrapper.DtoType}", Severity.Error);
    }
  }

  private async Task HandleInvokeCtrlAltDelClicked()
  {
    try
    {
      if (RemoteControlState.CurrentSession is not { } currentSession)
      {
        Snackbar.Add("No active remote session", Severity.Error);
        return;
      }

      var invokeResult = await ViewerHub.Server.InvokeCtrlAltDel(
        DeviceState.CurrentDevice.Id,
        currentSession.TargetProcessId,
        currentSession.DesktopSessionType);

      if (!invokeResult.IsSuccess)
      {
        Snackbar.Add($"Failed to send Ctrl+Alt+Del: {invokeResult.Reason}", Severity.Error);
        return;
      }

      Snackbar.Add("Ctrl+Alt+Del sent successfully", Severity.Success);
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error while invoking Ctrl+Alt+Del.");
      Snackbar.Add("An error occurred while sending Ctrl+Alt+Del", Severity.Error);
    }
  }

  private async Task HandleReceiveClipboardClicked()
  {
    try
    {
      if (RemoteControlState.CurrentSession is null)
      {
        Snackbar.Add("No active remote session", Severity.Error);
        return;
      }

      using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
      await RemoteControlStream.RequestClipboardText(RemoteControlState.CurrentSession.SessionId, cts.Token);
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error while handling clipboard change.");
      Snackbar.Add("An error occurred while receiving clipboard", Severity.Error);
    }
  }

  private async Task HandleSendClipboardClicked()
  {
    try
    {
      var text = await ClipboardManager.GetText();
      if (string.IsNullOrWhiteSpace(text))
      {
        Snackbar.Add("Clipboard is empty", Severity.Warning);
        return;
      }

      if (RemoteControlState.CurrentSession is null)
      {
        Snackbar.Add("No active remote session", Severity.Error);
        return;
      }

      Snackbar.Add("Sending clipboard", Severity.Info);
      using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
      await RemoteControlStream.SendClipboardText(text, RemoteControlState.CurrentSession.SessionId, cts.Token);
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error while sending clipboard.");
      Snackbar.Add("An error occurred while sending clipboard", Severity.Error);
    }
  }

  private async Task HandleTypeClipboardClicked()
  {
    try
    {
      var text = await ClipboardManager.GetText();
      if (string.IsNullOrWhiteSpace(text))
      {
        Snackbar.Add("Clipboard is empty", Severity.Warning);
        return;
      }

      Snackbar.Add("Sending clipboard to type", Severity.Info);
      using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
      await RemoteControlStream.SendTypeText(text, cts.Token);
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error while sending clipboard.");
      Snackbar.Add("An error occurred while sending clipboard", Severity.Error);
    }
  }
}
