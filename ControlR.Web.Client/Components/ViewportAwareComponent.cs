namespace ControlR.Web.Client.Components;

public class ViewportAwareComponent : DisposableComponent, IBrowserViewportObserver
{
  public Breakpoint CurrentBreakpoint { get; private set; }
  public Guid Id { get; } = Guid.NewGuid();
  [Inject]
  public required IBrowserViewportService ViewportService { get; init; }

  public async Task NotifyBrowserViewportChangeAsync(BrowserViewportEventArgs browserViewportEventArgs)
  {
    CurrentBreakpoint = browserViewportEventArgs.Breakpoint;
    await InvokeAsync(StateHasChanged);
  }

  protected override async ValueTask DisposeAsync(bool disposing)
  {
    await ViewportService.UnsubscribeAsync(this);
    await base.DisposeAsync(disposing);
  }

  protected override async Task OnInitializedAsync()
  {
    CurrentBreakpoint = await ViewportService.GetCurrentBreakpointAsync();
    await ViewportService.SubscribeAsync(this);
    await base.OnInitializedAsync();
  }
}
