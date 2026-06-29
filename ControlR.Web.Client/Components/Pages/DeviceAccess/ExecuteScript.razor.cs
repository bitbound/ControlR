using ControlR.Libraries.Api.Contracts.Dtos.ServerApi;
using ControlR.Libraries.Api.Contracts.Enums;
using ControlR.Libraries.Viewer.Common.State;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using MudBlazor;

namespace ControlR.Web.Client.Components.Pages.DeviceAccess;

public partial class ExecuteScript : ComponentBase, IDisposable
{
  [SupplyParameterFromQuery]
  public required Guid DeviceId { get; init; }

  [Inject]
  public required IDeviceState DeviceState { get; init; }

  [Inject]
  public required IControlrApi ControlrApi { get; init; }

  [Inject]
  public required IHubConnection<IViewerHub> ViewerHub { get; init; }

  [Inject]
  public required IMessenger Messenger { get; init; }

  [Inject]
  public required ISnackbar Snackbar { get; init; }

  private bool _loading = true;
  private bool _running;
  private List<ScriptDto> _scripts = [];
  private ScriptDto? _selectedScript;
  private ScriptRunAs _runAs = ScriptRunAs.System;

  private string _consoleOutput = string.Empty;
  private ScriptStatus? _executionStatus;
  private Guid? _currentExecutionId;

  protected override async Task OnInitializedAsync()
  {
    await base.OnInitializedAsync();
    Messenger.Register<DtoReceivedMessage<(Guid ExecutionId, string StdOutChunk, string StdErrChunk, bool IsFinished, int? ExitCode)>>(this, HandleScriptOutput);

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

  private async Task Execute()
  {
    if (_selectedScript is null)
    {
      return;
    }

    _running = true;
    _consoleOutput = string.Empty;
    _executionStatus = ScriptStatus.Running;

    try
    {
      var result = await ControlrApi.Scripts.ExecuteScript(_selectedScript.Id, [DeviceId], _runAs);

      if (result.IsSuccess && result.Value is not null && result.Value.Length > 0)
      {
        var exec = result.Value[0];
        _currentExecutionId = exec.Id;
        _consoleOutput = exec.StdErr; // If offline, will contain warning

        if (exec.Status == ScriptStatus.Running)
        {
          await ViewerHub.Server.WatchScriptExecution(exec.Id);
        }
        else
        {
          _executionStatus = exec.Status;
          _running = false;
        }
      }
      else
      {
        Snackbar.Add("Failed to start script execution.", Severity.Error);
        _executionStatus = ScriptStatus.Failed;
        _running = false;
      }
    }
    catch (Exception ex)
    {
      Snackbar.Add($"Error executing script: {ex.Message}", Severity.Error);
      _executionStatus = ScriptStatus.Failed;
      _running = false;
    }
  }

  private async Task HandleScriptOutput(object sender, DtoReceivedMessage<(Guid ExecutionId, string StdOutChunk, string StdErrChunk, bool IsFinished, int? ExitCode)> message)
  {
    var data = message.Dto;
    if (_currentExecutionId != data.ExecutionId)
    {
      return;
    }

    if (!string.IsNullOrEmpty(data.StdOutChunk))
    {
      _consoleOutput += data.StdOutChunk;
    }

    if (!string.IsNullOrEmpty(data.StdErrChunk))
    {
      _consoleOutput += $"[ERROR] {data.StdErrChunk}";
    }

    if (data.IsFinished)
    {
      _executionStatus = (data.ExitCode == 0) ? ScriptStatus.Succeeded : ScriptStatus.Failed;
      _running = false;
    }

    await InvokeAsync(StateHasChanged);
  }

  public void Dispose()
  {
    Messenger.UnregisterAll(this);
  }
}
