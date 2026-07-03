using Microsoft.JSInterop;

namespace ControlR.Web.Client.Components.Shared;

public partial class CodeView : JsInteropableComponent
{
  private ElementReference _containerElementRef;
  private string? _lastCodeContent;

  [Parameter]
  public string? CodeContent { get; set; }
  [Parameter]
  public EventCallback<string?> CodeContentChanged { get; set; }
  [Parameter]
  public bool IsEditable { get; set; }
  [Parameter]
  public CodeViewLanguage Language { get; set; }
  [Inject]
  public required IMessenger Messenger { get; init; }
  [Parameter]
  public bool ScrollToBottomOnLoad { get; set; }
  [Inject]
  public required IThemeStateProvider ThemeState { get; init; }

  private string LanguageString
  {
    get => Language switch
    {
      CodeViewLanguage.CSharp => "csharp",
      CodeViewLanguage.PowerShell => "powershell",
      CodeViewLanguage.Log => "log",
      _ => "plaintext"
    };
  }

  protected override async ValueTask DisposeAsync(bool disposing)
  {
    if (disposing)
    {
      Messenger.UnregisterAll(this);

      if (IsJsModuleReady)
      {
        await JsModule.InvokeVoidAsync("disposeMonacoEditor", _containerElementRef);
      }
    }
    await base.DisposeAsync(disposing);
  }

  protected override async Task OnAfterRenderAsync(bool firstRender)
  {
    await base.OnAfterRenderAsync(firstRender);

    if (!RendererInfo.IsInteractive)
    {
      return;
    }

    if (firstRender)
    {
      await WaitForJsModule();

      await JsModule.InvokeVoidAsync(
        "initMonacoEditor",
        _containerElementRef,
        CodeContent ?? string.Empty,
        LanguageString,
        IsEditable,
        ThemeState.CurrentThemeMode);


      if (ScrollToBottomOnLoad && !string.IsNullOrEmpty(CodeContent))
      {
        await JsModule.InvokeVoidAsync("scrollMonacoToBottom", _containerElementRef);
      }

      _lastCodeContent = CodeContent;
      return;
    }

    if (CodeContent != _lastCodeContent)
    {
      await WaitForJsModule();
      await JsModule.InvokeVoidAsync("updateMonacoContent", _containerElementRef, CodeContent ?? string.Empty);
      _lastCodeContent = CodeContent;
    }
  }

  protected override void OnInitialized()
  {
    base.OnInitialized();
    Messenger.Register<ThemeChangedMessage>(this, HandleThemeChanged);
  }

  private async Task HandleThemeChanged(object subscriber, ThemeChangedMessage message)
  {
    if (!RendererInfo.IsInteractive || !IsJsModuleReady)
    {
      return;
    }

    await JsModule.InvokeVoidAsync("updateMonacoTheme", message.ThemeMode);
  }
}

public enum CodeViewLanguage
{
  None,
  CSharp,
  PowerShell,
  Log
}
