namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi;

public record IssueTenantInviteRequestDto(
  Guid TenantId,
  string InviteeEmail);
