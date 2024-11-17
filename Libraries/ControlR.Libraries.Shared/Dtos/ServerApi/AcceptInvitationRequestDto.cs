namespace ControlR.Libraries.Shared.Dtos.ServerApi;
public record AcceptInvitationRequestDto(
  string ActivationCode,
  string Email,
  string Password);
