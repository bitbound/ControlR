namespace ControlR.Web.Client.Components.RemoteDisplays;

public partial class InputPopover : DisposableComponent
{
  private static readonly KeyShortcut[] _linuxKeyShortcuts =
  [
    new("Super", [], KeyStroke.Meta),
    new("Alt+Tab", [KeyStroke.Alt], KeyStroke.Tab),
  ];

  private static readonly KeyShortcut[] _macKeyShortcuts =
  [
    new("Cmd+Tab", [KeyStroke.Meta], KeyStroke.Tab),
    new("Cmd+Space", [KeyStroke.Meta], new(" ", "Space")),
  ];

  private static readonly KeyShortcut[] _windowsKeyShortcuts =
  [
    new("Win", [], KeyStroke.Meta),
    new("Win+R", [KeyStroke.Meta], new("r", "KeyR")),
    new("Alt+Tab", [KeyStroke.Alt], KeyStroke.Tab),
    new("Ctrl+Shift+Esc", [KeyStroke.Control, KeyStroke.Shift], new("Escape", "Escape")),
  ];

  private bool _isBlockInputDisabled;

  [Inject]
  public required IClipboardManager ClipboardManager { get; init; }

  [CascadingParameter]
  public Func<Task>? ClosePopover { get; set; }

  [Inject]
  public required IDeviceAccessPermissionsState DeviceAccessPermissionsState { get; init; }

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

  private KeyShortcut[] CurrentKeyShortcuts =>
    DeviceState.TryGetCurrentDevice()?.Platform switch
    {
      SystemPlatform.Windows => _windowsKeyShortcuts,
      SystemPlatform.Linux => _linuxKeyShortcuts,
      SystemPlatform.MacOs => _macKeyShortcuts,
      _ => []
    };

  protected override async Task OnAfterRenderAsync(bool firstRender)
  {
    if (firstRender)
    {
      Disposables.AddRange(
        RemoteControlState.OnStateChanged(() => InvokeAsync(StateHasChanged)),
        DeviceAccessPermissionsState.OnStateChanged(() => InvokeAsync(StateHasChanged)),
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

  private async Task HandleDtoReceived(RemoteControlDtoWrapper wrapper)
  {
    try
    {
      switch (wrapper.DtoType)
      {
        case RemoteControlDtoType.BlockInputResult:
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
      Logger.LogError(ex, "Error while handling received DTO of type {RemoteControlDtoType}.", wrapper.DtoType);
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

      var invokeResult = await ViewerHub.Server.InvokeCtrlAltDel2(new(
        DeviceState.CurrentDevice.Id,
        currentSession.TargetProcessId,
        currentSession.DesktopSessionType));

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

  private async Task HandleKeyboardInputModeChanged(KeyboardInputMode mode)
  {
    RemoteControlState.KeyboardInputMode = mode;
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
      var activityResult = await ViewerHub.Server.AddViewerActivity2(new(DeviceAccessActivityNames.ReceiveClipboardText));
      if (!activityResult.IsSuccess)
      {
        Logger.LogWarning("Failed to log ReceiveClipboardText activity: {Reason}", activityResult.Reason);
      }

      if (ClosePopover is not null)
      {
        await ClosePopover();
      }
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

      using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
      var sendResult = await RemoteControlStream.SendClipboardText(
        text, RemoteControlState.CurrentSession.SessionId, cts.Token);

      if (!sendResult.IsSuccess)
      {
        Snackbar.Add($"Failed to send clipboard: {sendResult.Reason}", Severity.Error);
        return;
      }

      Snackbar.Add("Clipboard sent", Severity.Success);

      var activityResult = await ViewerHub.Server.AddViewerActivity2(new(DeviceAccessActivityNames.SendClipboardText));
      if (!activityResult.IsSuccess)
      {
        Logger.LogWarning("Failed to log SendClipboardText activity: {Reason}", activityResult.Reason);
      }

      if (ClosePopover is not null)
      {
        await ClosePopover();
      }
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error while sending clipboard.");
      Snackbar.Add("An error occurred while sending clipboard", Severity.Error);
    }
  }

  private async Task HandleSendKeyShortcutClicked(KeyShortcut shortcut)
  {
    if (RemoteControlState.IsViewOnlyEnabled)
    {
      Snackbar.Add("Input is disabled due to view-only mode", Severity.Warning);
      return;
    }

    if (!RemoteControlStream.IsConnected)
    {
      Snackbar.Add("No active remote session", Severity.Error);
      return;
    }

    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
    var heldModifiers = new List<KeyStroke>();

    try
    {
      foreach (var modifier in shortcut.Modifiers)
      {
        heldModifiers.Add(modifier);
        await SendKeyStroke(modifier, true, heldModifiers, cts.Token);
      }

      await SendKeyStroke(shortcut.Key, true, heldModifiers, cts.Token);
      await SendKeyStroke(shortcut.Key, false, heldModifiers, cts.Token);

      for (var i = heldModifiers.Count - 1; i >= 0; i--)
      {
        var modifier = heldModifiers[i];
        heldModifiers.RemoveAt(i);
        await SendKeyStroke(modifier, false, heldModifiers, cts.Token);
      }
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error while sending key shortcut {Shortcut}.", shortcut.Label);
      Snackbar.Add($"An error occurred while sending {shortcut.Label}", Severity.Error);

      // Don't leave a modifier stuck down on the remote device.
      try
      {
        using var resetCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await RemoteControlStream.SendKeyboardStateReset(resetCts.Token);
      }
      catch (Exception resetEx)
      {
        Logger.LogError(resetEx, "Error while resetting keyboard state after a failed key shortcut.");
      }
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

      if (ClosePopover is not null)
      {
        await ClosePopover();
      }
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error while sending clipboard.");
      Snackbar.Add("An error occurred while sending clipboard", Severity.Error);
    }
  }

  private async Task SendKeyStroke(
    KeyStroke keyStroke,
    bool isPressed,
    IReadOnlyCollection<KeyStroke> heldModifiers,
    CancellationToken cancellationToken)
  {
    // Mirror the modifier flags a browser reports: a modifier's own keydown includes it, its keyup doesn't.
    var modifiers = new KeyEventModifiersDto(
      heldModifiers.Contains(KeyStroke.Control),
      heldModifiers.Contains(KeyStroke.Shift),
      heldModifiers.Contains(KeyStroke.Alt),
      heldModifiers.Contains(KeyStroke.Meta));

    await RemoteControlStream.SendKeyEvent(
      keyStroke.Key,
      keyStroke.Code,
      isPressed,
      RemoteControlState.KeyboardInputMode,
      modifiers,
      cancellationToken);
  }

  private sealed record KeyShortcut(string Label, KeyStroke[] Modifiers, KeyStroke Key);

  private sealed record KeyStroke(string Key, string Code)
  {
    public static readonly KeyStroke Alt = new("Alt", "AltLeft");
    public static readonly KeyStroke Control = new("Control", "ControlLeft");
    public static readonly KeyStroke Meta = new("Meta", "MetaLeft");
    public static readonly KeyStroke Shift = new("Shift", "ShiftLeft");
    public static readonly KeyStroke Tab = new("Tab", "Tab");
  }
}
