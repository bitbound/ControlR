using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.ApiClient.Interfaces.Internal;

public interface IInvitesApi
{
  [ApiRoute($"{HttpConstants.Internal.InvitesEndpoint}/accept", "POST")]
  Task<ApiResult<AcceptInvitationResponseDto>> AcceptInvitation(AcceptInvitationRequestDto request, CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.InvitesEndpoint}", "POST")]
  [Obsolete("Use ControlrApi.V1.Invites.CreateInvite (POST /api/v1/invites?tenantId=), which requires tenantId as a query parameter. This internal route is unversioned and slated for removal.")]
  Task<ApiResult<TenantInviteResponseDto>> CreateTenantInvite(TenantInviteRequestDto request, CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.InvitesEndpoint}/{{inviteId}}", "DELETE")]
  [Obsolete("Use ControlrApi.V1.Invites.DeleteInvite (DELETE /api/v1/invites/{inviteId}?tenantId=), which requires tenantId as a query parameter. This internal route is unversioned and slated for removal.")]
  Task<ApiResult> DeleteTenantInvite(Guid inviteId, CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.InvitesEndpoint}", "GET")]
  [Obsolete("Use ControlrApi.V1.Invites.GetInvites (GET /api/v1/invites?tenantId=), which requires tenantId, returns an Items envelope, and renames each element from TenantInviteResponseDto to InviteResponseDto. This internal route is unversioned and slated for removal.")]
  Task<ApiResult<TenantInviteResponseDto[]>> GetPendingTenantInvites(CancellationToken cancellationToken = default);
}
