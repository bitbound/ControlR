using System.Net.Http.Json;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi;

using ControlR.Libraries.Api.Contracts.Enums;

namespace ControlR.ApiClient;

public partial class ControlrApi
{
  async Task<ApiResult<ScriptDto>> IScriptsApi.CreateScript(ScriptCreateRequestDto request, CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
    {
      using var response = await _client.PostAsJsonAsync(HttpConstants.ScriptsEndpoint, request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<ScriptDto>(cancellationToken);
    });
  }

  async Task<ApiResult<ScriptDto[]>> IScriptsApi.GetAllScripts(CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
      await _client.GetFromJsonAsync<ScriptDto[]>(HttpConstants.ScriptsEndpoint, cancellationToken));
  }

  async Task<ApiResult<ScriptDto>> IScriptsApi.GetScript(Guid id, CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
      await _client.GetFromJsonAsync<ScriptDto>($"{HttpConstants.ScriptsEndpoint}/{id}", cancellationToken));
  }

  async Task<ApiResult<ScriptDto>> IScriptsApi.UpdateScript(Guid id, ScriptCreateRequestDto request, CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
    {
      using var response = await _client.PutAsJsonAsync($"{HttpConstants.ScriptsEndpoint}/{id}", request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<ScriptDto>(cancellationToken);
    });
  }

  async Task<ApiResult> IScriptsApi.DeleteScript(Guid id, CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
    {
      using var response = await _client.DeleteAsync($"{HttpConstants.ScriptsEndpoint}/{id}", cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
    });
  }

  async Task<ApiResult<ScriptExecutionDto[]>> IScriptsApi.ExecuteScript(Guid id, Guid[] deviceIds, ScriptRunAs runAs, CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
    {
      using var response = await _client.PostAsJsonAsync($"{HttpConstants.ScriptsEndpoint}/{id}/execute?runAs={runAs}", deviceIds, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<ScriptExecutionDto[]>(cancellationToken);
    });
  }

  async Task<ApiResult<ScriptExecutionDto[]>> IScriptsApi.ExecuteAdHocScript(ExecuteScriptRequestDto request, CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
    {
      using var response = await _client.PostAsJsonAsync($"{HttpConstants.ScriptsEndpoint}/execute-adhoc", request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<ScriptExecutionDto[]>(cancellationToken);
    });
  }

  async Task<ApiResult<ScriptExecutionDto>> IScriptsApi.GetScriptExecution(Guid executionId, CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
      await _client.GetFromJsonAsync<ScriptExecutionDto>($"{HttpConstants.ScriptsEndpoint}/executions/{executionId}", cancellationToken));
  }

  async Task<ApiResult<ScriptExecutionDto[]>> IScriptsApi.GetAllExecutions(CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
      await _client.GetFromJsonAsync<ScriptExecutionDto[]>($"{HttpConstants.ScriptsEndpoint}/executions", cancellationToken));
  }
}
