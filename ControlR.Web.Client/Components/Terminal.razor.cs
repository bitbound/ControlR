﻿using System.Collections.Concurrent;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace ControlR.Web.Client.Components;

public partial class Terminal : IAsyncDisposable
{
  private readonly Dictionary<string, object> _inputAttributes = new()
  {
    ["autocapitalize"] = "off",
    ["spellcheck"] = "false"
  };

  private readonly ConcurrentList<string> _inputHistory = [];
  private readonly string _inputElementId = $"terminal-input-{Guid.NewGuid()}";
  private bool _enableMultiline;
  private bool _taboutPrevented;
  private MudTextField<string>? _inputElement;
  private int _inputHistoryIndex;
  private string _inputText = string.Empty;
  private bool _loading = true;
  private ElementReference _terminalOutputContainer;

  [CascadingParameter]
  public required DeviceContentInstance ContentInstance { get; init; }

  [Parameter]
  [EditorRequired]
  public required DeviceViewModel Device { get; init; }

  [Parameter]
  [EditorRequired]
  public required Guid Id { get; init; }

  [Inject]
  public required IJsInterop JsInterop { get; init; }

  [Inject]
  public required ILogger<Terminal> Logger { get; init; }

  [Inject]
  public required IMessenger Messenger { get; init; }

  [Inject]
  public required ISnackbar Snackbar { get; init; }

  [Inject]
  public required IViewerHubConnection ViewerHub { get; init; }

  [Inject]
  public required IDeviceContentWindowStore WindowStore { get; init; }

  private int InputLineCount => _enableMultiline ? 6 : 1;

  private ConcurrentQueue<TerminalOutputDto> Output { get; } = [];

  public async ValueTask DisposeAsync()
  {
    try
    {
      GC.SuppressFinalize(this);
      await ViewerHub.CloseTerminalSession(Device.Id, Id);
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error while disposing of terminal session.");
    }
  }

  protected override async Task OnInitializedAsync()
  {
    try
    {
      await base.OnInitializedAsync();

      Messenger.Register<DtoReceivedMessage<TerminalOutputDto>>(this, HandleTerminalOutputMessage);

      var result = await ViewerHub.CreateTerminalSession(Device.Id, Id);
      if (!result.IsSuccess)
      {
        Snackbar.Add("Failed to start terminal", Severity.Error);
        Logger.LogResult(result);
        WindowStore.Remove(ContentInstance);
      }
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error while initializing Terminal component.");
      Snackbar.Add("Terminal initialization error", Severity.Error);
      WindowStore.Remove(ContentInstance);
    }
    finally
    {
      _loading = false;
    }
  }

  protected override async Task OnAfterRenderAsync(bool firstRender)
  {
    await base.OnAfterRenderAsync(firstRender);

    if (_inputElement is not null && !_taboutPrevented)
    {
      _taboutPrevented = true;
      await JsInterop.PreventTabOut(_inputElementId);
    }
  }

  private static string GetOutputColor(TerminalOutputDto output)
  {
    return output.OutputKind switch
    {
      TerminalOutputKind.StandardOutput => "",
      TerminalOutputKind.StandardError => "mud-error-text",
      _ => ""
    };
  }

  private string GetTerminalHistory(bool forward)
  {
    if (_inputHistory.Count == 0)
    {
      return "";
    }

    if (forward && _inputHistoryIndex < _inputHistory.Count)
    {
      _inputHistoryIndex++;
    }
    else if (!forward && _inputHistoryIndex > 0)
    {
      _inputHistoryIndex--;
    }

    if (_inputHistoryIndex < 0 || _inputHistoryIndex >= _inputHistory.Count)
    {
      return "";
    }

    return _inputHistory.ElementAt(_inputHistoryIndex);
  }

  private async Task HandleTerminalOutputMessage(object subscriber, DtoReceivedMessage<TerminalOutputDto> message)
  {
    var dto = message.Dto;

    if (dto.TerminalId != Id)
    {
      return;
    }

    while (Output.Count > 500)
    {
      _ = Output.TryDequeue(out _);
    }

    Output.Enqueue(dto);
    await InvokeAsync(StateHasChanged);

    await JsInterop.ScrollToEnd(_terminalOutputContainer);
  }

  private async Task OnInputKeyUp(KeyboardEventArgs args)
  {
    if (_inputElement is null)
    {
      return;
    }

    if (!_enableMultiline && args.Key.Equals("ArrowUp", StringComparison.OrdinalIgnoreCase))
    {
      _inputText = GetTerminalHistory(false);
      return;
    }

    if (!_enableMultiline && args.Key.Equals("ArrowDown", StringComparison.OrdinalIgnoreCase))
    {
      _inputText = GetTerminalHistory(true);
      return;
    }

    if (args.Key == "Enter")
    {
      if (string.IsNullOrWhiteSpace(_inputText))
      {
        return;
      }

      if (args.CtrlKey || args.ShiftKey)
      {
        return;
      }

      try
      {
        while (_inputHistory.Count > 500)
        {
          _inputHistory.RemoveAt(0);
        }

        _inputText = _inputText.Trim();
        _inputHistory.Add(_inputText);
        _inputHistoryIndex = _inputHistory.Count;

        var result = await ViewerHub.SendTerminalInput(Device.Id, Id, _inputText);
        if (!result.IsSuccess)
        {
          Snackbar.Add(result.Reason, Severity.Error);
        }

        _inputText = string.Empty;
      }
      catch (Exception ex)
      {
        Logger.LogError(ex, "Error while sending terminal input.");
        Snackbar.Add("An error occurred", Severity.Error);
      }
    }
  }
}