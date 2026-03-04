using System.ComponentModel.DataAnnotations;

namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi;

public record TenantInviteRequestDto(
  [EmailAddress]
  string InviteeEmail);