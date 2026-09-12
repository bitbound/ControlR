namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.Invites;

public record InviteResponseDto(
  Guid Id,
  DateTimeOffset CreatedAt,
  string InviteeEmail,
  Uri InviteUrl);
