using System.Diagnostics.CodeAnalysis;

namespace ControlR.Libraries.Shared.Dtos.ServerApi;
public record AcceptInvitationResponseDto(
  [property: MemberNotNullWhen(false, nameof(AcceptInvitationResponseDto.ErrorMessage))]
  bool IsSuccessful,
  string? ErrorMessage = null);
