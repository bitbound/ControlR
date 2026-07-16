using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.ApiClient.Interfaces.Internal;

public interface IInvitesApi
{
  [ApiRoute($"{HttpConstants.Internal.InvitesEndpoint}/accept", "POST")]
  Task<ApiResult<AcceptInvitationResponseDto>> AcceptInvitation(AcceptInvitationRequestDto request, CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.InvitesEndpoint}", "POST")]
  Task<ApiResult<TenantInviteResponseDto>> CreateTenantInvite(TenantInviteRequestDto request, CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.InvitesEndpoint}/{{inviteId}}", "DELETE")]
  Task<ApiResult> DeleteTenantInvite(Guid inviteId, CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.InvitesEndpoint}", "GET")]
  Task<ApiResult<TenantInviteResponseDto[]>> GetPendingTenantInvites(CancellationToken cancellationToken = default);
}
