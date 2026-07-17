namespace ControlR.Web.Client.Components.Dialogs;

public partial class PersonalAccessTokenDialog
{
  [Inject]
  public required IClipboardManager ClipboardManager { get; set; }
  [CascadingParameter]
  public required IMudDialogInstance MudDialog { get; init; }
  [Parameter]
  public required PersonalAccessTokenResponseDto PersonalAccessToken { get; set; }
  [Parameter]
  public required string PlainTextKey { get; set; }
  [Inject]
  public required ISnackbar Snackbar { get; set; }

  private async Task CopyToClipboard()
  {
    try
    {
      await ClipboardManager.SetText(PlainTextKey);
      Snackbar.Add("Personal access token copied to clipboard", Severity.Success);
    }
    catch (Exception ex)
    {
      Snackbar.Add($"Failed to copy to clipboard: {ex.Message}", Severity.Error);
    }
  }

  private void Submit()
  {
    MudDialog.Close(DialogResult.Ok(true));
  }
}
