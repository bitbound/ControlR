using ControlR.Libraries.Api.Contracts.Dtos.ServerApi;
using ControlR.Libraries.Api.Contracts.Enums;
using ControlR.Web.Client.StateManagement.Stores;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using MudBlazor;
using System.Collections.Concurrent;

namespace ControlR.Web.Client.Components.Dialogs;

public partial class RunScriptDialog : ComponentBase, IDisposable
{
  [CascadingParameter]
  public required IMudDialogInstance MudDialog { get; init; }

  [Parameter]
  public ScriptDto? Script { get; set; }

  [Parameter]
  public List<DeviceResponseDto>? TargetDevices { get; set; }

  [Parameter]
  public ScriptRunAs RunAs { get; set; } = ScriptRunAs.System;

  [Parameter]
  public bool AutoExecute { get; set; }

  // Inject dependencies
  [Inject]
  public required IControlrApi ControlrApi { get; init; }

  [Inject]
  public required IHubConnection<IViewerHub> ViewerHub { get; init; }

  [Inject]
  public required IMessenger Messenger { get; init; }

  [Inject]
  public required ISnackbar Snackbar { get; init; }

  [Inject]
  public required IDeviceStore DeviceStore { get; init; }

  // State fields
  private bool _loading = true;
  private bool _running;
  private List<DeviceResponseDto> _allDevices = [];
  private HashSet<DeviceResponseDto> _selectedDevices = [];
  private ScriptRunAs _runAs = ScriptRunAs.System;

  // Running executions tracking
  private List<ScriptExecutionDto> _executions = [];
  private readonly ConcurrentDictionary<Guid, System.Text.StringBuilder> _outputs = new();
  private ScriptExecutionDto? _selectedExecution;
  private Guid? _selectedDeviceExecutionId;

  protected override async Task OnInitializedAsync()
  {
    await base.OnInitializedAsync();
    Messenger.Register<DtoReceivedMessage<(Guid ExecutionId, string StdOutChunk, string StdErrChunk, bool IsFinished, int? ExitCode)>>(this, HandleScriptOutput);

    _runAs = RunAs;

    try
    {
      await DeviceStore.Refresh();
      _allDevices = [.. DeviceStore.Items];

      if (TargetDevices is not null && TargetDevices.Count > 0)
      {
        // Match target devices to the ones in _allDevices to preserve reference equality if needed
        _selectedDevices = _allDevices.Where(d => TargetDevices.Any(td => td.Id == d.Id)).ToHashSet();
      }
    }
    catch (Exception ex)
    {
      Snackbar.Add($"Failed to load devices: {ex.Message}", Severity.Error);
    }
    finally
    {
      _loading = false;
    }

    if (AutoExecute && _selectedDevices.Count > 0)
    {
      await Execute();
    }
  }

  private async Task Execute()
  {
    if (_selectedDevices.Count == 0)
    {
      Snackbar.Add("Please select at least one device.", Severity.Warning);
      return;
    }

    if (Script is null)
    {
      return;
    }

    _running = true;
    _executions = [];
    _outputs.Clear();

    try
    {
      var deviceIds = _selectedDevices.Select(x => x.Id).ToArray();
      var result = await ControlrApi.Scripts.ExecuteScript(Script.Id, deviceIds, _runAs);

      if (result.IsSuccess && result.Value is not null)
      {
        _executions = [.. result.Value];
        
        foreach (var exec in _executions)
        {
          _outputs[exec.Id] = new System.Text.StringBuilder(exec.StdErr); // Initialize with offline/startup logs if any
          
          if (exec.Status == ScriptStatus.Running)
          {
            // Subscribe to real-time SignalR logs
            await ViewerHub.Server.WatchScriptExecution(exec.Id);
          }
        }

        if (_executions.Count > 0)
        {
          _selectedExecution = _executions[0];
          _selectedDeviceExecutionId = _executions[0].Id;
        }
      }
      else
      {
        Snackbar.Add("Failed to start script execution.", Severity.Error);
        _running = false;
      }
    }
    catch (Exception ex)
    {
      Snackbar.Add($"Error starting execution: {ex.Message}", Severity.Error);
      _running = false;
    }
  }

  private async Task HandleScriptOutput(object sender, DtoReceivedMessage<(Guid ExecutionId, string StdOutChunk, string StdErrChunk, bool IsFinished, int? ExitCode)> message)
  {
    var data = message.Dto;
    if (!_outputs.ContainsKey(data.ExecutionId))
    {
      return;
    }

    var sb = _outputs[data.ExecutionId];

    if (!string.IsNullOrEmpty(data.StdOutChunk))
    {
      sb.Append(data.StdOutChunk);
    }

    if (!string.IsNullOrEmpty(data.StdErrChunk))
    {
      sb.Append("[ERROR] ").Append(data.StdErrChunk);
    }

    // Update execution status in local list
    var idx = _executions.FindIndex(x => x.Id == data.ExecutionId);
    if (idx != -1)
    {
      var current = _executions[idx];
      var newStatus = current.Status;
      if (data.IsFinished)
      {
        newStatus = (data.ExitCode == 0) ? ScriptStatus.Succeeded : ScriptStatus.Failed;
      }

      _executions[idx] = current with {
        Status = newStatus,
        ExitCode = data.ExitCode,
        FinishedAt = data.IsFinished ? DateTimeOffset.Now : null
      };
    }

    await InvokeAsync(StateHasChanged);
  }

  private void OnSelectedExecutionChanged(ScriptExecutionDto val)
  {
    _selectedExecution = val;
    _selectedDeviceExecutionId = val?.Id;
  }

  private void Close()
  {
    MudDialog.Close(DialogResult.Ok(true));
  }

  public void Dispose()
  {
    Messenger.UnregisterAll(this);
  }
}
