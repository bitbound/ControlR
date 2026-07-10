using ControlR.Libraries.Api.Contracts.Dtos;

namespace ControlR.ApiClient.Interfaces.Internal;

public interface ITestEmailApi
{
  Task<ApiResult> SendTestEmail(CancellationToken cancellationToken = default);
}
