using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;

namespace ControlR.ApiClient.Interfaces.V1;

public interface ITestEmailApi
{
  [ApiRoute($"{HttpConstants.V1.TestEmailEndpoint}", "POST")]
  Task<ApiResult> SendTestEmail(CancellationToken cancellationToken = default);
}
