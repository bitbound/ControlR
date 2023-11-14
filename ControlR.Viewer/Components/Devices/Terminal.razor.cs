using ControlR.Shared.Dtos;
using ControlR.Viewer.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using MudBlazor;

namespace ControlR.Viewer.Components.Devices;

public partial class Terminal
{
    private readonly Guid _terminalId = Guid.NewGuid();
    private bool _loading = true;

    [Parameter, EditorRequired]
    public required DeviceDto Device { get; init; }

    [Inject]
    public required ILogger<Terminal> Logger { get; init; }

    [Inject]
    public required ISnackbar Snackbar { get; init; }

    [Inject]
    public required IViewerHubConnection ViewerHub { get; init; }

    protected override async Task OnInitializedAsync()
    {
        try
        {
            await base.OnInitializedAsync();

            var result = await ViewerHub.CreateTerminalSession(Device.ConnectionId, _terminalId);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error while initializing Terminal component.");
            Snackbar.Add("Terminal initialization error", Severity.Error);
        }
        finally
        {
            _loading = false;
        }
    }
}