using ControlR.Libraries.Api.Contracts.Dtos.ServerApi;
using ControlR.Libraries.Api.Contracts.Enums;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace ControlR.Web.Client.Components.Pages;

public partial class Scripts : ComponentBase
{
  private List<ScriptDto> _scripts = [];
  private bool _loading = true;

  // Form fields for create/edit
  private bool _isEditing;
  private Guid? _editingScriptId;
  private string _scriptName = string.Empty;
  private string _scriptDescription = string.Empty;
  private string _scriptCode = string.Empty;
  private ShellType _shellType = ShellType.PowerShell;
  private int _timeoutSeconds = 300;

  [Inject]
  public required IControlrApi ControlrApi { get; init; }

  [Inject]
  public required ISnackbar Snackbar { get; init; }

  [Inject]
  public required IDialogService DialogService { get; init; }

  protected override async Task OnInitializedAsync()
  {
    await base.OnInitializedAsync();
    await LoadScripts();
  }

  private async Task LoadScripts()
  {
    _loading = true;
    try
    {
      var result = await ControlrApi.Scripts.GetAllScripts();
      if (result.IsSuccess && result.Value is not null)
      {
        _scripts = [.. result.Value];
      }
      else
      {
        Snackbar.Add("Failed to load scripts.", Severity.Error);
      }
    }
    catch (Exception ex)
    {
      Snackbar.Add($"Error: {ex.Message}", Severity.Error);
    }
    finally
    {
      _loading = false;
    }
  }

  private void OpenCreateForm()
  {
    _isEditing = true;
    _editingScriptId = null;
    _scriptName = string.Empty;
    _scriptDescription = string.Empty;
    _scriptCode = string.Empty;
    _shellType = ShellType.PowerShell;
    _timeoutSeconds = 300;
  }

  private void OpenEditForm(ScriptDto script)
  {
    _isEditing = true;
    _editingScriptId = script.Id;
    _scriptName = script.Name;
    _scriptDescription = script.Description;
    _scriptCode = script.CodeContent;
    _shellType = script.ShellType;
    _timeoutSeconds = script.TimeoutSeconds;
  }

  private void CancelEdit()
  {
    _isEditing = false;
  }

  private async Task SaveScript()
  {
    if (string.IsNullOrWhiteSpace(_scriptName) || string.IsNullOrWhiteSpace(_scriptCode))
    {
      Snackbar.Add("Name and script content are required.", Severity.Warning);
      return;
    }

    try
    {
      var request = new ScriptCreateRequestDto(
        _scriptName,
        _scriptDescription,
        _scriptCode,
        _shellType,
        _timeoutSeconds);

      ApiResult<ScriptDto> result;

      if (_editingScriptId.HasValue)
      {
        result = await ControlrApi.Scripts.UpdateScript(_editingScriptId.Value, request);
      }
      else
      {
        result = await ControlrApi.Scripts.CreateScript(request);
      }

      if (result.IsSuccess)
      {
        Snackbar.Add("Script saved successfully.", Severity.Success);
        _isEditing = false;
        await LoadScripts();
      }
      else
      {
        Snackbar.Add("Failed to save script.", Severity.Error);
      }
    }
    catch (Exception ex)
    {
      Snackbar.Add($"Error: {ex.Message}", Severity.Error);
    }
  }

  private async Task DeleteScript(Guid id)
  {
    var confirmed = await DialogService.ShowMessageBoxAsync(
      "Confirm Delete",
      "Are you sure you want to delete this script?",
      yesText: "Delete", cancelText: "Cancel");

    if (confirmed != true)
    {
      return;
    }

    try
    {
      var result = await ControlrApi.Scripts.DeleteScript(id);
      if (result.IsSuccess)
      {
        Snackbar.Add("Script deleted successfully.", Severity.Success);
        await LoadScripts();
      }
      else
      {
        Snackbar.Add("Failed to delete script.", Severity.Error);
      }
    }
    catch (Exception ex)
    {
      Snackbar.Add($"Error: {ex.Message}", Severity.Error);
    }
  }

  private async Task OpenRunDialog(ScriptDto script)
  {
    var parameters = new DialogParameters<RunScriptDialog>
    {
      { x => x.Script, script }
    };

    var options = new DialogOptions { CloseButton = true, MaxWidth = MaxWidth.Medium, FullWidth = true };
    await DialogService.ShowAsync<RunScriptDialog>($"Run Script: {script.Name}", parameters, options);
  }
}
