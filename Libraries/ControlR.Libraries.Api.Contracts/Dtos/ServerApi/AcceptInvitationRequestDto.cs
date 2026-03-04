namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi;
public record AcceptInvitationRequestDto(
  string ActivationCode,
  string Email,
  string Password);
