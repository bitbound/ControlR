using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;

namespace ControlR.ApiClient.Interfaces.Internal;

public interface ITestEmailApi
{
  [ApiRoute("POST", "/api/internal/test-email")]
  Task<ApiResult> SendTestEmail(CancellationToken cancellationToken = default);
}
