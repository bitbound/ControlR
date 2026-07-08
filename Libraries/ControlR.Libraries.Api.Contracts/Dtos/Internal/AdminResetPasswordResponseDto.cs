using ControlR.Libraries.DataRedaction;

namespace ControlR.Libraries.Api.Contracts.Dtos.Internal;

public record AdminResetPasswordResponseDto(
  [ProtectedDataClassification]
  string TemporaryPassword);
