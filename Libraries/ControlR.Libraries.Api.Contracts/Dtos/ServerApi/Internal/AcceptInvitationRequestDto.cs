namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

public record AcceptInvitationRequestDto(
  string ActivationCode,
  string Email,
  string Password);
