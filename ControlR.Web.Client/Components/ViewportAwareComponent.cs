using Microsoft.AspNetCore.Components;

namespace ControlR.Web.Client.Components;

public class ViewportAwareComponent : ComponentBase, IBrowserViewportObserver, IAsyncDisposable
{
  public Breakpoint CurrentBreakpoint { get; private set; }

  public Guid Id { get; } = Guid.NewGuid();

  [Inject]
  public required IBrowserViewportService ViewportService { get; init; }

  public virtual async ValueTask DisposeAsync()
  {
    await ViewportService.UnsubscribeAsync(this);
    GC.SuppressFinalize(this);
  }

  public async Task NotifyBrowserViewportChangeAsync(BrowserViewportEventArgs browserViewportEventArgs)
  {
    CurrentBreakpoint = browserViewportEventArgs.Breakpoint;
    await InvokeAsync(StateHasChanged);
  }

  protected override async Task OnInitializedAsync()
  {
    CurrentBreakpoint = await ViewportService.GetCurrentBreakpointAsync();
    await ViewportService.SubscribeAsync(this);
    await base.OnInitializedAsync();
  }
}
