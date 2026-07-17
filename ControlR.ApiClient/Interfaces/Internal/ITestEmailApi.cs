using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;

namespace ControlR.ApiClient.Interfaces.Internal;

public interface ITestEmailApi
{
  [ApiRoute($"{HttpConstants.Internal.TestEmailEndpoint}", "POST")]
  Task<ApiResult> SendTestEmail(CancellationToken cancellationToken = default);
}
