namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Public;
public record AcceptInvitationRequestDto(
  string ActivationCode,
  string Email,
  string Password);
