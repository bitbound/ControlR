using ControlR.Libraries.Api.Contracts.Enums;
namespace ControlR.Libraries.Api.Contracts.Dtos.Ui;

[MessagePackObject]
public record ToastInfo(
  [property: Key(0)] string Message,
  [property: Key(1)] MessageSeverity MessageSeverity);
