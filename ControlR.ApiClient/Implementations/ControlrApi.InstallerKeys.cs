using System.Net.Http.Json;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.HubDtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi;

namespace ControlR.ApiClient;

public partial class ControlrApi
{
  private static string AppendQueryParameter(string url, string name, object? value)
  {
    if (value is null)
    {
      return url;
    }

    var separator = url.Contains('?') ? '&' : '?';
    return $"{url}{separator}{name}={Uri.EscapeDataString(value.ToString() ?? string.Empty)}";
  }

  async Task<ApiResult<CreateInstallerKeyResponseDto>> IInstallerKeysApi.CreateInstallerKey(CreateInstallerKeyRequestDto dto, CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
    {
      using var response = await _client.PostAsJsonAsync(HttpConstants.InstallerKeysEndpoint, dto, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<CreateInstallerKeyResponseDto>(cancellationToken);
    });
  }

  async Task<ApiResult> IInstallerKeysApi.DeleteInstallerKey(Guid keyId, Guid? tenantId, Guid? userId, bool? isTenantAdmin, CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
    {
      var url = $"{HttpConstants.InstallerKeysEndpoint}/{keyId}";
      url = AppendQueryParameter(url, nameof(tenantId), tenantId);
      url = AppendQueryParameter(url, nameof(userId), userId);
      url = AppendQueryParameter(url, nameof(isTenantAdmin), isTenantAdmin);
      using var response = await _client.DeleteAsync(url, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
    });
  }

  async Task<ApiResult<AgentInstallerKeyDto[]>> IInstallerKeysApi.GetAllInstallerKeys(Guid? tenantId, Guid? userId, bool? isTenantAdmin, CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
      {
        var url = HttpConstants.InstallerKeysEndpoint;
        url = AppendQueryParameter(url, nameof(tenantId), tenantId);
        url = AppendQueryParameter(url, nameof(userId), userId);
        url = AppendQueryParameter(url, nameof(isTenantAdmin), isTenantAdmin);
        return await _client.GetFromJsonAsync<AgentInstallerKeyDto[]>(url, cancellationToken);
      });
  }

  async Task<ApiResult<AgentInstallerKeyUsageDto[]>> IInstallerKeysApi.GetInstallerKeyUsages(Guid keyId, Guid? tenantId, Guid? userId, bool? isTenantAdmin, CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
      {
        var url = $"{HttpConstants.InstallerKeysEndpoint}/usages/{keyId}";
        url = AppendQueryParameter(url, nameof(tenantId), tenantId);
        url = AppendQueryParameter(url, nameof(userId), userId);
        url = AppendQueryParameter(url, nameof(isTenantAdmin), isTenantAdmin);
        return await _client.GetFromJsonAsync<AgentInstallerKeyUsageDto[]>(url, cancellationToken);
      });
  }

  async Task<ApiResult> IInstallerKeysApi.IncrementInstallerKeyUsage(Guid keyId, Guid? deviceId, CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
    {
      var url = deviceId.HasValue
        ? $"{HttpConstants.InstallerKeysEndpoint}/increment-usage/{keyId}?deviceId={deviceId.Value}"
        : $"{HttpConstants.InstallerKeysEndpoint}/increment-usage/{keyId}";

      using var response = await _client.PostAsync(url, null, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
    });
  }

  async Task<ApiResult> IInstallerKeysApi.RenameInstallerKey(RenameInstallerKeyRequestDto dto, CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
    {
      using var response = await _client.PutAsJsonAsync($"{HttpConstants.InstallerKeysEndpoint}/rename", dto, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
    });
  }
}
