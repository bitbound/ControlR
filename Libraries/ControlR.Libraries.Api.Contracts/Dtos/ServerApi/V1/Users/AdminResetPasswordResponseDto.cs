using ControlR.Libraries.DataRedaction;

namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.Users;

public record AdminResetPasswordResponseDto(
  [ProtectedDataClassification]
  string TemporaryPassword);
