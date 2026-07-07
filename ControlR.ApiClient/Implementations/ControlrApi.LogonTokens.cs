using System.Net.Http.Json;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi;

namespace ControlR.ApiClient;

public partial class ControlrApi
{
	async Task<ApiResult<LogonTokenResponseDto>> ILogonTokensApi.CreateLogonToken(LogonTokenRequestDto request, CancellationToken cancellationToken)
	{
		return await ExecuteApiCall(async () =>
		{
      using var response = await _client.PostAsJsonAsync(HttpConstants.Internal.LogonTokensEndpoint, request, cancellationToken);
     await response.EnsureSuccessStatusCodeWithDetails();
			return await response.Content.ReadFromJsonAsync<LogonTokenResponseDto>(cancellationToken);
		});
	}

	async Task<ApiResult<LogonTokenResponseDto>> ILogonTokensApi.IssueLogonToken(IssueLogonTokenRequestDto request, CancellationToken cancellationToken)
	{
		return await ExecuteApiCall(async () =>
		{
      using var response = await _client.PostAsJsonAsync(HttpConstants.V1.LogonTokensEndpoint, request, cancellationToken);
     await response.EnsureSuccessStatusCodeWithDetails();
			return await response.Content.ReadFromJsonAsync<LogonTokenResponseDto>(cancellationToken);
		});
	}
}
