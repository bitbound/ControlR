using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace ControlR.Viewer.Components;
public class ViewportAwareComponent : ComponentBase, IBrowserViewportObserver
{

    [Inject]
    public required IBrowserViewportService ViewportService { get; init; }
    public Breakpoint CurrentBreakpoint { get; private set; }

    public Guid Id { get; } = Guid.NewGuid();

    protected override async Task OnInitializedAsync()
    {
        CurrentBreakpoint = await ViewportService.GetCurrentBreakpointAsync();
        await ViewportService.SubscribeAsync(this);
        await base.OnInitializedAsync();
    }

    public async Task NotifyBrowserViewportChangeAsync(BrowserViewportEventArgs browserViewportEventArgs)
    {
        CurrentBreakpoint = browserViewportEventArgs.Breakpoint;
        await InvokeAsync(StateHasChanged);
    }
}
