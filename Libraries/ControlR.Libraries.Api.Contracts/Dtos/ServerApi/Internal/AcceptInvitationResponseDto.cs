using System.Diagnostics.CodeAnalysis;

namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

public record AcceptInvitationResponseDto(
  [property: MemberNotNullWhen(false, nameof(AcceptInvitationResponseDto.ErrorMessage))]
  bool IsSuccessful,
  string? ErrorMessage = null);
