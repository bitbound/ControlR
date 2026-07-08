using System.ComponentModel.DataAnnotations;

namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

public record TenantInviteRequestDto(
  [EmailAddress]
  string InviteeEmail);