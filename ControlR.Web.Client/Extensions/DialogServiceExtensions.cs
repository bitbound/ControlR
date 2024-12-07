namespace ControlR.Web.Client.Extensions;

public static class DialogServiceExtensions
{
  public static async Task<string?> ShowPrompt(
    this IDialogService dialogService,
    string title,
    string? subtitle = null,
    string? inputHintText = null,
    MaxWidth maxWidth = MaxWidth.Small)
  {
    inputHintText ??= "Enter your response here.";

    var dialogOptions = new DialogOptions
    {
      BackdropClick = false,
      FullWidth = true,
      MaxWidth = maxWidth
    };

    var parameters = new DialogParameters
    {
      { nameof(PromptDialog.Title), title },
      { nameof(PromptDialog.Subtitle), subtitle },
      { nameof(PromptDialog.InputHintText), inputHintText }
    };

    var dialogRef = await dialogService.ShowAsync<PromptDialog>(title, parameters, dialogOptions);
    var result = await dialogRef.Result;
    if (result?.Data is string { } response)
    {
      return response;
    }

    return null;
  }
}
