namespace ControlR.Libraries.Api.Contracts.Dtos.Internal;
public record TenantInviteResponseDto(
  Guid Id,
  DateTimeOffset CreatedAt,
  string InviteeEmail,
  Uri InviteUrl
  );
