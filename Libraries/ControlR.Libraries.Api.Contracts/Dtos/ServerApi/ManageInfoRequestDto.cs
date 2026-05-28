namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi;

public record ManageInfoRequestDto(
  string? NewEmail,
  string? NewPassword,
  string? OldPassword);