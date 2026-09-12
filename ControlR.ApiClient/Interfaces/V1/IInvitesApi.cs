using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.Invites;

namespace ControlR.ApiClient.Interfaces.V1;

public interface IInvitesApi
{
  [ApiRoute($"{HttpConstants.V1.InvitesEndpoint}?tenantId={{tenantId}}", "POST")]
  Task<ApiResult<InviteResponseDto>> CreateInvite(Guid tenantId, CreateInviteRequestDto request, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.InvitesEndpoint}/{{inviteId}}?tenantId={{tenantId}}", "DELETE")]
  Task<ApiResult> DeleteInvite(Guid inviteId, Guid tenantId, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.InvitesEndpoint}?tenantId={{tenantId}}", "GET")]
  Task<ApiResult<InvitesResponseDto>> GetInvites(Guid tenantId, CancellationToken cancellationToken = default);
}
