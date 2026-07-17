using System.ComponentModel.DataAnnotations;

namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

public record ManageInfoResponseDto(
  string Email,
  bool IsEmailConfirmed);