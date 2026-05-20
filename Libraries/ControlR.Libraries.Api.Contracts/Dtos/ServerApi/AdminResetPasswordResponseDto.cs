using ControlR.Libraries.DataRedaction;

namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi;

public record AdminResetPasswordResponseDto(
  [ProtectedDataClassification]
  string TemporaryPassword);
