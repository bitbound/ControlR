namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;
public record InviteResponseDto(
  Guid Id,
  DateTimeOffset CreatedAt,
  string InviteeEmail,
  Uri InviteUrl
  );
