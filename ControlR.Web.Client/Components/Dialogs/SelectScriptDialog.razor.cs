using ControlR.Libraries.Api.Contracts.Dtos.ServerApi;
using ControlR.Libraries.Api.Contracts.Enums;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace ControlR.Web.Client.Components.Dialogs;

public partial class SelectScriptDialog : ComponentBase
{
  [CascadingParameter]
  public required IMudDialogInstance MudDialog { get; init; }

  [Parameter]
  public DeviceResponseDto? Device { get; set; }

  [Inject]
  public required IControlrApi ControlrApi { get; init; }

  [Inject]
  public required ISnackbar Snackbar { get; init; }

  private bool _loading = true;
  private List<ScriptDto> _scripts = [];
  private ScriptDto? _selectedScript;
  private ScriptRunAs _runAs = ScriptRunAs.System;

  protected override async Task OnInitializedAsync()
  {
    await base.OnInitializedAsync();

    try
    {
      var result = await ControlrApi.Scripts.GetAllScripts();
      if (result.IsSuccess && result.Value is not null)
      {
        _scripts = [.. result.Value];
        if (_scripts.Count > 0)
        {
          _selectedScript = _scripts[0];
        }
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

  private void Confirm()
  {
    if (_selectedScript is null)
    {
      return;
    }

    MudDialog.Close(DialogResult.Ok((_selectedScript, _runAs)));
  }

  private void Cancel()
  {
    MudDialog.Cancel();
  }
}
