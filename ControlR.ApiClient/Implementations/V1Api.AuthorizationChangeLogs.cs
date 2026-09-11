using System.Net.Http.Json;
using ControlR.ApiClient.Interfaces.V1;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ACLDtos = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.AuthorizationChangeLogs;

namespace ControlR.ApiClient;

internal partial class V1Api
{
  private static string BuildAuthorizationChangeLogsQuery(
    Guid? tenantId,
    int page,
    int pageSize,
    string? actionType,
    string? actorType,
    string? targetType,
    string? searchText,
    DateTimeOffset? from,
    DateTimeOffset? to)
  {
    var parameters = new List<string>
    {
      $"page={Uri.EscapeDataString(page.ToString())}",
      $"pageSize={Uri.EscapeDataString(pageSize.ToString())}"
    };

    if (tenantId.HasValue)
    {
      parameters.Insert(0, $"tenantId={Uri.EscapeDataString(tenantId.Value.ToString())}");
    }

    if (!string.IsNullOrWhiteSpace(actionType))
    {
      parameters.Add($"actionType={Uri.EscapeDataString(actionType)}");
    }

    if (!string.IsNullOrWhiteSpace(actorType))
    {
      parameters.Add($"actorType={Uri.EscapeDataString(actorType)}");
    }

    if (!string.IsNullOrWhiteSpace(targetType))
    {
      parameters.Add($"targetType={Uri.EscapeDataString(targetType)}");
    }

    if (!string.IsNullOrWhiteSpace(searchText))
    {
      parameters.Add($"searchText={Uri.EscapeDataString(searchText)}");
    }

    if (from.HasValue)
    {
      parameters.Add($"from={Uri.EscapeDataString(from.Value.ToString("O"))}");
    }

    if (to.HasValue)
    {
      parameters.Add($"to={Uri.EscapeDataString(to.Value.ToString("O"))}");
    }

    return $"?{string.Join("&", parameters)}";
  }

  async Task<ApiResult<ACLDtos.AuthorizationChangeLogsResponseDto>> IAuthorizationChangeLogsApi.GetAuthorizationChangeLogs(
    Guid tenantId,
    int page,
    int pageSize,
    string? actionType,
    string? actorType,
    string? targetType,
    string? searchText,
    DateTimeOffset? from,
    DateTimeOffset? to,
    CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      var query = BuildAuthorizationChangeLogsQuery(
        tenantId, page, pageSize, actionType, actorType, targetType, searchText, from, to);

      return await _client.HttpClient.GetFromJsonAsync<ACLDtos.AuthorizationChangeLogsResponseDto>(
        $"{HttpConstants.V1.AuthorizationChangeLogsEndpoint}{query}", cancellationToken)
        ?? throw new InvalidOperationException("Empty response from authorization change logs endpoint.");
    });
  }

  async Task<ApiResult<ACLDtos.AuthorizationChangeLogsResponseDto>> IAuthorizationChangeLogsApi.GetServerAuthorizationChangeLogs(
    int page,
    int pageSize,
    string? actionType,
    string? actorType,
    string? targetType,
    string? searchText,
    DateTimeOffset? from,
    DateTimeOffset? to,
    CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      var query = BuildAuthorizationChangeLogsQuery(
        null, page, pageSize, actionType, actorType, targetType, searchText, from, to);

      return await _client.HttpClient.GetFromJsonAsync<ACLDtos.AuthorizationChangeLogsResponseDto>(
        $"{HttpConstants.V1.AuthorizationChangeLogsEndpoint}/server{query}", cancellationToken)
        ?? throw new InvalidOperationException("Empty response from server authorization change logs endpoint.");
    });
  }
}