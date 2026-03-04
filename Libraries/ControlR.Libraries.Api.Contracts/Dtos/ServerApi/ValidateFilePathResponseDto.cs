namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi;

[MessagePackObject(keyAsPropertyName: true)]
public record ValidateFilePathResponseDto(
  bool IsValid,
  string ErrorMessage = "");
