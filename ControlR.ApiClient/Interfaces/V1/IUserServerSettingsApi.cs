using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.UserServerSettings;

namespace ControlR.ApiClient.Interfaces.V1;

public interface IUserServerSettingsApi
{
  [ApiRoute($"{HttpConstants.V1.UserServerSettingsEndpoint}/decommission-status", "GET")]
  Task<ApiResult<DecommissionServerResponseDto>> GetDecommissionStatus(CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.UserServerSettingsEndpoint}/file-upload-max-size", "GET")]
  Task<ApiResult<FileUploadMaxSizeResponseDto>> GetFileUploadMaxSize(CancellationToken cancellationToken = default);
}
