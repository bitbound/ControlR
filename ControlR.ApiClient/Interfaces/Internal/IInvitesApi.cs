using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.ApiClient.Interfaces.Internal;

public interface IInvitesApi
{
  [ApiRoute("POST", "/api/internal/invites/accept")]
  Task<ApiResult<AcceptInvitationResponseDto>> AcceptInvitation(AcceptInvitationRequestDto request, CancellationToken cancellationToken = default);
  [ApiRoute("POST", "/api/internal/invites")]
  Task<ApiResult<TenantInviteResponseDto>> CreateTenantInvite(TenantInviteRequestDto request, CancellationToken cancellationToken = default);
  [ApiRoute("DELETE", "/api/internal/invites/{inviteId}")]
  Task<ApiResult> DeleteTenantInvite(Guid inviteId, CancellationToken cancellationToken = default);
  [ApiRoute("GET", "/api/internal/invites")]
  Task<ApiResult<TenantInviteResponseDto[]>> GetPendingTenantInvites(CancellationToken cancellationToken = default);
}
