using System.Net.Http.Json;
using ControlR.ApiClient.Interfaces.Internal;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using InternalDtos = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.ApiClient;

internal partial class InternalApi
{
  private static string BuildAuthorizationChangeLogsQuery(
    int page,
    int pageSize,
    string? actionType,
    string? actorType,
    string? targetType,
    string? searchText,
    Guid? tenantId,
    DateTimeOffset? from,
    DateTimeOffset? to)
  {
    var parameters = new List<string>
    {
      $"page={Uri.EscapeDataString(page.ToString())}",
      $"pageSize={Uri.EscapeDataString(pageSize.ToString())}"
    };

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

    if (tenantId.HasValue)
    {
      parameters.Add($"tenantId={Uri.EscapeDataString(tenantId.Value.ToString())}");
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

  async Task<ApiResult<InternalDtos.AuthorizationChangeLogSearchResponseDto>> IAuthorizationChangeLogsApi.Get(
    int page,
    int pageSize,
    string? actionType,
    string? actorType,
    string? targetType,
    string? searchText,
    Guid? tenantId,
    DateTimeOffset? from,
    DateTimeOffset? to,
    CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      var query = BuildAuthorizationChangeLogsQuery(
        page, pageSize, actionType, actorType, targetType, searchText, tenantId, from, to);

      return await _client.HttpClient.GetFromJsonAsync<InternalDtos.AuthorizationChangeLogSearchResponseDto>(
        $"{HttpConstants.Internal.AuthorizationChangeLogsEndpoint}{query}", cancellationToken)
        ?? throw new InvalidOperationException("Empty response from authorization change logs endpoint.");
    });
  }
}
