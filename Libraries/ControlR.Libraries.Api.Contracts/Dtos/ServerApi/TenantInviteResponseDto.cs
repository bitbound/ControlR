namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi;
public record TenantInviteResponseDto(
  Guid Id,
  DateTimeOffset CreatedAt,
  string InviteeEmail,
  Uri InviteUrl
  );
