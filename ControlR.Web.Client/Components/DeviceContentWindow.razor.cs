using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace ControlR.Web.Client.Components;

public partial class DeviceContentWindow : IAsyncDisposable
{
  [Parameter, EditorRequired]
  public required DeviceContentInstance ContentInstance { get; init; }

  [Inject]
  public required ISystemEnvironment EnvironmentHelper { get; init; }

  [Inject]
  public required IJsInterop JsInterop { get; init; }

  [Inject]
  public required IJSRuntime JsRuntime { get; init; }

  [Inject]
  public required ILogger<DeviceContentWindow> Logger { get; init; }

  [Inject]
  public required IMessenger Messenger { get; init; }

  [Inject]
  public required ISnackbar Snackbar { get; init; }

  [Inject]
  public required IViewerHubConnection ViewerHub { get; init; }

  public WindowState WindowState { get; private set; } = WindowState.Restored;

  [Inject]
  public required IDeviceContentWindowStore WindowStore { get; init; }

  public ValueTask DisposeAsync()
  {
    Messenger.UnregisterAll(this);
    GC.SuppressFinalize(this);
    return ValueTask.CompletedTask;
  }

  protected override async Task OnInitializedAsync()
  {
    if (await JsInterop.IsTouchScreen())
    {
      WindowState = WindowState.Maximized;
    }

    Messenger.Register<DeviceContentWindowStateMessage>(this, HandleDeviceContentWindowStateChanged);

    await base.OnInitializedAsync();
  }

  private async Task Close()
  {
    WindowStore.Remove(ContentInstance);
    await DisposeAsync();
  }

  private async Task HandleDeviceContentWindowStateChanged(object subscriber, DeviceContentWindowStateMessage message)
  {
    if (message.WindowId == ContentInstance.WindowId)
    {
      return;
    }

    if (message.State != WindowState.Minimized)
    {
      WindowState = WindowState.Minimized;
      await InvokeAsync(StateHasChanged);
    }
  }

  private void SetWindowState(WindowState state)
  {
    WindowState = state;
    Messenger.Send(new DeviceContentWindowStateMessage(ContentInstance.WindowId, state));
  }
}