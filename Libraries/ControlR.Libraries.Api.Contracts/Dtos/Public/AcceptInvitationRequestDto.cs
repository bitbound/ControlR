namespace ControlR.Libraries.Api.Contracts.Dtos.Public;
public record AcceptInvitationRequestDto(
  string ActivationCode,
  string Email,
  string Password);
