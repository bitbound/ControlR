using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using InviteDtos = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.Invites;

namespace ControlR.ApiClient.Interfaces.V1;

public interface IInvitesApi
{
  [ApiRoute($"{HttpConstants.V1.InvitesEndpoint}?tenantId={{tenantId}}", "POST")]
  Task<ApiResult<InviteDtos.InviteResponseDto>> CreateInvite(Guid tenantId, InviteDtos.CreateInviteRequestDto request, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.InvitesEndpoint}/{{inviteId}}?tenantId={{tenantId}}", "DELETE")]
  Task<ApiResult> DeleteInvite(Guid inviteId, Guid tenantId, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.InvitesEndpoint}?tenantId={{tenantId}}", "GET")]
  Task<ApiResult<InviteDtos.InvitesResponseDto>> GetInvites(Guid tenantId, CancellationToken cancellationToken = default);
}
