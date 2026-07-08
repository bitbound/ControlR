using ControlR.Libraries.DataRedaction;

namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

public record AdminResetPasswordResponseDto(
  [ProtectedDataClassification]
  string TemporaryPassword);
