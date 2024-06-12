using ControlR.Viewer.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using MudBlazor;
using System.Collections.Concurrent;

namespace ControlR.Viewer.Components;

public partial class Terminal : IAsyncDisposable
{
    private readonly Dictionary<string, object> _inputAttributes = new()
    {
        ["autocapitalize"] = "off",
        ["spellcheck"] = "false"
    };

    private readonly ConcurrentList<string> _inputHistory = [];
    private bool _enableMultiline;
    private MudTextField<string>? _inputElement;
    private int _inputHistoryIndex;
    private string _inputText = string.Empty;
    private bool _loading = true;
    private ElementReference _terminalOutputContainer;

    [CascadingParameter]
    public required DeviceContentInstance ContentInstance { get; init; }

    [Parameter, EditorRequired]
    public required DeviceDto Device { get; init; }

    [Parameter, EditorRequired]
    public required Guid Id { get; init; }

    [Inject]
    public required IJsInterop JsInterop { get; init; }

    [Inject]
    public required IJSRuntime JsRuntime { get; init; }

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

    private int InputLineCount
    {
        get
        {
            return _enableMultiline ? 6 : 1;
        }
    }

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

            Messenger.Register<TerminalOutputMessage>(this, HandleTerminalOutputMessage);

            var result = await ViewerHub.CreateTerminalSession(Device.ConnectionId, Id);
            if (!result.IsSuccess)
            {
                Snackbar.Add("Failed to start terminal", Severity.Error);
                Logger.LogResult(result);
                WindowStore.Remove(ContentInstance);
                return;
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

    private static string GetOutputColor(TerminalOutputDto output)
    {
        return output.OutputKind switch
        {
            TerminalOutputKind.StandardOutput => "",
            TerminalOutputKind.StandardError => "mud-error-text",
            _ => "",
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

    private async Task HandleTerminalOutputMessage(object subscriber, TerminalOutputMessage message)
    {
        if (message.OutputDto.TerminalId != Id)
        {
            return;
        }

        while (Output.Count > 500)
        {
            _ = Output.TryDequeue(out _);
        }

        Output.Enqueue(message.OutputDto);
        await InvokeAsync(StateHasChanged);

        await JsInterop.ScrollToEnd(_terminalOutputContainer);
    }

    private async Task OnInputKeyDown(KeyboardEventArgs args)
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

                var result = await ViewerHub.SendTerminalInput(Device.ConnectionId, Id, _inputText);
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