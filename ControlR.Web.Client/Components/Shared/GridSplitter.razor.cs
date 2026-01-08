using Microsoft.JSInterop;

namespace ControlR.Web.Client.Components.Shared;

public partial class GridSplitter : JsInteropableComponent
{
  private ElementReference _containerRef;
  private ElementReference _leftPanelRef;
  private ElementReference _rightPanelRef;
  private ElementReference _splitterRef;

  [Parameter]
  public int InitialLeftPanelWidth { get; set; } = 300;
  [Parameter]
  public RenderFragment? LeftPanel { get; set; }
  [Inject]
  public required ILogger<GridSplitter> Logger { get; set; }
  [Parameter]
  public int MinLeftPanelWidth { get; set; } = 200;
  [Parameter]
  public int MinRightPanelWidth { get; set; } = 300;
  [Parameter]
  public RenderFragment? RightPanel { get; set; }
  [Parameter]
  public int SplitterWidth { get; set; } = 4;

  protected override async ValueTask DisposeAsync(bool disposing)
  {
    try
    {
      if (disposing)
      {
        if (IsJsModuleReady)
        {
          await JsModule.InvokeVoidAsync("dispose");
        }
      }

      await base.DisposeAsync(disposing);
    }
    catch (JSDisconnectedException)
    {
      Logger.LogDebug("JavaScript module already disconnected during dispose");
    }
  }

  protected override async Task OnAfterRenderAsync(bool firstRender)
  {
    await base.OnAfterRenderAsync(firstRender);

    if (firstRender)
    {
      await WaitForJsModule();
      await JsModule.InvokeVoidAsync(
        "initializeGridSplitter",
        _containerRef,
        _splitterRef,
        _leftPanelRef,
        _rightPanelRef,
        MinLeftPanelWidth,
        MinRightPanelWidth);
    }
  }

  private string GetGridTemplateColumns()
  {
    return $"minmax({MinLeftPanelWidth}px, {InitialLeftPanelWidth}px) {SplitterWidth}px 1fr";
  }
}
