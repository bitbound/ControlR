namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

public record ManageInfoRequestDto(
  string? NewEmail,
  string? NewPassword,
  string? OldPassword);