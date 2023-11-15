using Bitbound.SimpleMessenger;
using ControlR.Shared.Dtos;
using ControlR.Shared.Extensions;
using ControlR.Viewer.Models;
using ControlR.Viewer.Models.Messages;
using ControlR.Viewer.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using MudBlazor;
using System.Collections.Concurrent;

namespace ControlR.Viewer.Components.Devices;

public partial class Terminal : IAsyncDisposable
{
    private readonly ConcurrentQueue<TerminalOutputDto> _output = [];
    private bool _loading = true;

    [CascadingParameter]
    public required DeviceContentInstance ContentInstance { get; init; }

    [Parameter, EditorRequired]
    public required DeviceDto Device { get; init; }

    [Parameter, EditorRequired]
    public required Guid Id { get; init; }

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

    public async ValueTask DisposeAsync()
    {
        try
        {
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

    private async Task HandleTerminalOutputMessage(TerminalOutputMessage message)
    {
        if (message.OutputDto.TerminalId != Id)
        {
            return;
        }
        _output.Enqueue(message.OutputDto);
        await InvokeAsync(StateHasChanged);
    }
}