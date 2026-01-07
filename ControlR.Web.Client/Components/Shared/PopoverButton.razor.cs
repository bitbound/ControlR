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
  [EditorRequired]
  public required string Icon { get; set; }
  [Parameter]
  [EditorRequired]
  public required string Label { get; set; }
  [Parameter]
  [EditorRequired]
  public required string TooltipText { get; set; }

  [JSInvokable]
  public void ClosePopover()
  {
    if (_isOpen)
    {
      _isOpen = false;
      StateHasChanged();
    }
  }

  public override async ValueTask DisposeAsync()
  {
    if (IsJsModuleReady)
    {
      await JsModule.InvokeVoidAsync("dispose");
    }
    _componentRef?.Dispose();
    await base.DisposeAsync();
    GC.SuppressFinalize(this);
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
