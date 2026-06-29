using ControlR.Libraries.Api.Contracts.Dtos.ServerApi;
using ControlR.Libraries.Api.Contracts.Enums;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace ControlR.Web.Client.Components.Pages;

public partial class ScriptLogs : ComponentBase
{
  private List<ScriptExecutionDto> _executions = [];
  private bool _loading = true;

  // Selected execution detail modal/view
  private ScriptExecutionDto? _selectedExecution;
  private bool _detailOpen;

  [Inject]
  public required IControlrApi ControlrApi { get; init; }

  [Inject]
  public required ISnackbar Snackbar { get; init; }

  protected override async Task OnInitializedAsync()
  {
    await base.OnInitializedAsync();
    await LoadExecutions();
  }

  private async Task LoadExecutions()
  {
    _loading = true;
    try
    {
      var result = await ControlrApi.Scripts.GetAllExecutions();
      if (result.IsSuccess && result.Value is not null)
      {
        _executions = [.. result.Value];
      }
      else
      {
        Snackbar.Add("Failed to load execution history.", Severity.Error);
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

  private void OpenDetail(ScriptExecutionDto execution)
  {
    _selectedExecution = execution;
    _detailOpen = true;
  }

  private void CloseDetail()
  {
    _detailOpen = false;
    _selectedExecution = null;
  }
}
