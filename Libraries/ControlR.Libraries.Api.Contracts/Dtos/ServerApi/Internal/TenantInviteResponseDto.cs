namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;
public record TenantInviteResponseDto(
  Guid Id,
  DateTimeOffset CreatedAt,
  string InviteeEmail,
  Uri InviteUrl
  );
