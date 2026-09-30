using Microsoft.JSInterop;

namespace ControlR.Web.Client.Components.Pages;

public partial class NotFound : JsInteropableComponent
{
  [Parameter]
  public bool HideActionButtons { get; set; }

  private async Task GoBack()
  {
    await WaitForJsModule(ComponentClosing);
    await JsModule.InvokeVoidAsync("goBack", ComponentClosing);
  }
}
