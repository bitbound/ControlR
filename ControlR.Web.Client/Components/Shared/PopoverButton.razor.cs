using Microsoft.JSInterop;

namespace ControlR.Web.Client.Components.Shared;

public partial class PopoverButton : JsInteropableComponent
{
  private readonly string _elementId = $"popover-{Guid.NewGuid()}";

  private DotNetObjectReference<PopoverButton>? _componentRef;
  private bool _isOpen;

  [Parameter]
  public RenderFragment? ChildContent { get; set; }
  [Parameter]
  public string? EndIcon { get; set; }
  [Parameter]
  public bool HideLabelOnMobile { get; set; }
  [Parameter]
  [EditorRequired]
  public required string Label { get; set; }
  [Parameter]
  public string? StartIcon { get; set; }
  [Parameter]
  [EditorRequired]
  public required string TooltipText { get; set; }
  [Parameter]
  public Variant Variant { get; set; } = Variant.Outlined;

  [JSInvokable]
  public void ClosePopover()
  {
    if (_isOpen)
    {
      _isOpen = false;
      StateHasChanged();
    }
  }

  protected override async ValueTask DisposeAsync(bool disposing)
  {
    if (disposing)
    {
      if (IsJsModuleReady)
      {
        await JsModule.InvokeVoidAsync("dispose");
      }
      _componentRef?.Dispose();
    }
    await base.DisposeAsync(disposing);
}

  protected override async Task OnAfterRenderAsync(bool firstRender)
  {
    await base.OnAfterRenderAsync(firstRender);

    if (firstRender)
    {
      _componentRef = DotNetObjectReference.Create(this);
      await JsModule.InvokeVoidAsync("initialize", _componentRef, _elementId);
    }
  }

  private void TogglePopover()
  {
    _isOpen = !_isOpen;
  }
}
