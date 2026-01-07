using Microsoft.JSInterop;

namespace ControlR.Web.Client.Components.Shared;

public partial class CodeView : JsInteropableComponent
{
  private ElementReference _codeElementRef;
  private string? _lastFormattedContent;

  [Parameter]
  public string? CodeContent { get; set; }

  [Parameter]
  public EventCallback<string?> CodeContentChanged { get; set; }

  [Parameter]
  public bool IsEditable { get; set; }

  [Parameter]
  public CodeViewLanguage Language { get; set; }

  [Parameter]
  public bool ShowLineBorders { get; set; } = true;

  private Guid CodeViewerId { get; } = Guid.NewGuid();
  private string LanguageString
  {
    get => Language switch
    {
      CodeViewLanguage.CSharp => "language-csharp",
      CodeViewLanguage.PowerShell => "language-powershell",
      CodeViewLanguage.Log => "language-log",
      _ => "language-plaintext"
    };
  }

  protected override async Task OnAfterRenderAsync(bool firstRender)
  {
    await base.OnAfterRenderAsync(firstRender);

    if (CodeContent != _lastFormattedContent)
    {
      await JsRuntime.InvokeVoidAsync("Prism.highlightElement", _codeElementRef);
      _lastFormattedContent = CodeContent;
    }
  }

  private async Task HandleCodeContentChanged()
  {
    await UpdateCodeContent();
  }

  private async Task HandleCodeElementBlurred()
  {
    await UpdateCodeContent();
  }

  private async Task UpdateCodeContent()
  {
    if (!IsEditable)
    {
      return;
    }

    var updatedText = await JsModule.InvokeAsync<string?>("getElementText", _codeElementRef);

    if (updatedText != CodeContent)
    {
      CodeContent = updatedText;
      await CodeContentChanged.InvokeAsync(updatedText);
    }
  }
}

public enum CodeViewLanguage
{
  None,
  CSharp,
  PowerShell,
  Log
}