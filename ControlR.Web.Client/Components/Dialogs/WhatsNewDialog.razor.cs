namespace ControlR.Web.Client.Components.Dialogs;

public partial class WhatsNewDialog
{
  private bool _hasError;
  private string _htmlContent = string.Empty;
  private bool _isLoading = true;

  [Inject]
  public required IControlrApi ControlrApi { get; init; }
  [Parameter]
  [EditorRequired]
  public required string CurrentVersion { get; init; }
  [Inject]
  public required ILogger<WhatsNewDialog> Logger { get; init; }
  [Inject]
  public required IMarkdownParser MarkdownParser { get; init; }
  [CascadingParameter]
  public required IMudDialogInstance MudDialog { get; init; }

  protected override async Task OnInitializedAsync()
  {
    await base.OnInitializedAsync();
    await LoadReleaseNotes();
  }

  private void Dismiss()
  {
    MudDialog.Close(DialogResult.Ok(true));
  }

  private async Task LoadReleaseNotes()
  {
    try
    {
      var result = await ControlrApi.Version.GetReleaseNotes(ComponentClosing);
      if (result.IsSuccess && !string.IsNullOrWhiteSpace(result.Value))
      {
        _htmlContent = MarkdownParser.ToHtml(result.Value);
      }
      else
      {
        _hasError = true;
      }
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Failed to load release notes.");
      _hasError = true;
    }
    finally
    {
      _isLoading = false;
    }
  }
}
