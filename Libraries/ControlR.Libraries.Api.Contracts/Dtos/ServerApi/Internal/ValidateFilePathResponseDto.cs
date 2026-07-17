namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

[MessagePackObject(keyAsPropertyName: true)]
public record ValidateFilePathResponseDto(
  bool IsValid,
  string ErrorMessage = "");
