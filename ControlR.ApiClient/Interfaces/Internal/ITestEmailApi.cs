using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;

namespace ControlR.ApiClient.Interfaces.Internal;

public interface ITestEmailApi
{
  [ApiRoute($"{HttpConstants.Internal.TestEmailEndpoint}", "POST")]
  [Obsolete("Use ControlrApi.V1.TestEmail.SendTestEmail (POST /api/v1/test-email), which returns the same value. This internal route is unversioned and slated for removal.")]
  Task<ApiResult> SendTestEmail(CancellationToken cancellationToken = default);
}
