using System.ComponentModel.DataAnnotations;

namespace ControlR.Libraries.Shared.Dtos.ServerApi;

public record TenantInviteRequestDto(
  [EmailAddress]
  string InviteeEmail);