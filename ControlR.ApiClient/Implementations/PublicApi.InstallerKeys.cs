using ControlR.Libraries.Api.Contracts.Dtos;

namespace ControlR.ApiClient;

internal partial class PublicApi
{
  async Task<ApiResult> IPublicInstallerKeysApi.IncrementInstallerKeyUsage(Guid keyId, Guid? deviceId, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      var url = deviceId.HasValue
        ? $"public/installer-keys/increment-usage/{keyId}?deviceId={deviceId.Value}"
        : $"public/installer-keys/increment-usage/{keyId}";

      using var response = await _client.HttpClient.PostAsync(url, null, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
    });
  }
}