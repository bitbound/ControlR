namespace ControlR.Libraries.Shared.Dtos.ServerApi;

[MessagePackObject(keyAsPropertyName: true)]
public record ValidateFilePathResponseDto(
  bool IsValid,
  string ErrorMessage = "");
