using System.ComponentModel.DataAnnotations;

namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.Invites;

public record CreateInviteRequestDto(
  [property: EmailAddress]
  string InviteeEmail);
