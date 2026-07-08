using System.ComponentModel.DataAnnotations;

namespace ControlR.Libraries.Api.Contracts.Dtos.Internal;

public record TenantInviteRequestDto(
  [EmailAddress]
  string InviteeEmail);