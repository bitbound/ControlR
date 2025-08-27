using ControlR.Libraries.Shared.Dtos.StreamerDtos;
using ControlR.Libraries.Shared.Services.Http;
using ControlR.Web.Client.ViewModels;
using Microsoft.AspNetCore.Components;

namespace ControlR.Web.Client.Components.Dialogs;

public partial class DesktopPreviewDialog : ComponentBase
{
  private bool _isLoading = true;
  private string? _errorMessage;
  private string? _previewImageDataUri;

  [CascadingParameter]
  public required IMudDialogInstance MudDialog { get; set; }

  [Parameter]
  [EditorRequired]
  public required DeviceDto Device { get; set; }

  [Parameter]
  [EditorRequired]
  public required DeviceUiSession Session { get; set; }

  [Inject]
  public required IControlrApi ControlrApi { get; init; }

  [Inject]
  public required ILogger<DesktopPreviewDialog> Logger { get; init; }

  [Inject]
  public required ISnackbar Snackbar { get; init; }

  public bool IsLoading => _isLoading;
  public string? ErrorMessage => _errorMessage;
  public string? PreviewImageDataUri => _previewImageDataUri;

  protected override async Task OnInitializedAsync()
  {
    await base.OnInitializedAsync();
    await LoadPreview();
  }

  private async Task LoadPreview()
  {
    try
    {
      _isLoading = true;
      _errorMessage = null;
      _previewImageDataUri = null;
      await InvokeAsync(StateHasChanged);

      var result = await ControlrApi.GetDesktopPreview(Device.Id, Session.ProcessId);
      
      if (result.IsSuccess && result.Value.Length > 0)
      {
        var base64Image = Convert.ToBase64String(result.Value);
        _previewImageDataUri = $"data:image/jpeg;base64,{base64Image}";
      }
      else
      {
        _errorMessage = result.IsSuccess ? "No preview image received" : result.Reason;
        Logger.LogError("Failed to get desktop preview: {Reason}", _errorMessage);
      }
    }
    catch (Exception ex)
    {
      _errorMessage = "An error occurred while loading the preview";
      Logger.LogError(ex, "Error while loading desktop preview for device {DeviceId}, session {SessionId}", 
        Device.Id, Session.SystemSessionId);
    }
    finally
    {
      _isLoading = false;
      await InvokeAsync(StateHasChanged);
    }
  }

  private async Task RefreshPreview()
  {
    await LoadPreview();
    Snackbar.Add("Preview refreshed", Severity.Info);
  }

  private void Close()
  {
    MudDialog.Close(DialogResult.Ok(false));
  }
}
