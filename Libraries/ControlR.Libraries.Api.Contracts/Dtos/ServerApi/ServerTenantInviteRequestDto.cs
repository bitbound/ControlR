using System.ComponentModel.DataAnnotations;

namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi;

public record ServerTenantInviteRequestDto(
  Guid TenantId,
  [EmailAddress] string InviteeEmail);