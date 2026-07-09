namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

public record IssueTenantInviteRequestDto(
  Guid TenantId,
  string InviteeEmail);
