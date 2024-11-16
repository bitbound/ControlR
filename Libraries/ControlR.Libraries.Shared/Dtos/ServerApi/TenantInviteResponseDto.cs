namespace ControlR.Libraries.Shared.Dtos.ServerApi;
public record TenantInviteResponseDto(
  Guid Id,
  DateTimeOffset CreatedAt,
  string InviteeEmail,
  Uri InviteUrl
  ) : IHasPrimaryKey;
