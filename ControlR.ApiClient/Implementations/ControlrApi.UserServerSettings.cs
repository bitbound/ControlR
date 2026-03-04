using System.Net.Http.Json;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi;

namespace ControlR.ApiClient;

public partial class ControlrApi
{
  async Task<ApiResult<long>> IUserServerSettingsApi.GetFileUploadMaxSize(CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
    {
      using var response = await _client.GetAsync($"{HttpConstants.UserServerSettingsEndpoint}/file-upload-max-size", cancellationToken);
      response.EnsureSuccessStatusCode();
      var dto = await response.Content.ReadFromJsonAsync<FileUploadMaxSizeResponseDto>(cancellationToken)
        ?? throw new HttpRequestException("The server response was empty.");
      return dto.MaxFileSize;
    });
  }
}
