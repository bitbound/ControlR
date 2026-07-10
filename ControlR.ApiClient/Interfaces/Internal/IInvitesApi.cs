using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.ApiClient.Interfaces.Internal;

public interface IInvitesApi
{
  Task<ApiResult<AcceptInvitationResponseDto>> AcceptInvitation(AcceptInvitationRequestDto request, CancellationToken cancellationToken = default);
  Task<ApiResult<TenantInviteResponseDto>> CreateTenantInvite(TenantInviteRequestDto request, CancellationToken cancellationToken = default);
  Task<ApiResult> DeleteTenantInvite(Guid inviteId, CancellationToken cancellationToken = default);
  Task<ApiResult<TenantInviteResponseDto[]>> GetPendingTenantInvites(CancellationToken cancellationToken = default);
}
