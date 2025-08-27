using Microsoft.AspNetCore.Components;

namespace ControlR.Web.Client.Components.Dialogs;

public partial class PersonalAccessTokenDialog
{
  [CascadingParameter]
  public required IMudDialogInstance MudDialog { get; init; }

  [Parameter]
  public required PersonalAccessTokenDto PersonalAccessToken { get; set; }

  [Parameter]
  public required string PlainTextKey { get; set; }

  [Inject]
  private IClipboardManager ClipboardManager { get; set; } = default!;

  [Inject]
  private ISnackbar Snackbar { get; set; } = default!;

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
