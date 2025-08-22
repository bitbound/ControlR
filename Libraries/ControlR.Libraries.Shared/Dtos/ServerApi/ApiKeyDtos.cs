using System.ComponentModel.DataAnnotations;

namespace ControlR.Libraries.Shared.Dtos.ServerApi;

public record ApiKeyDto(
  Guid Id,
  string FriendlyName,
  DateTimeOffset CreatedAt,
  DateTimeOffset? LastUsed);

public record CreateApiKeyRequestDto(
  [Required]
  [StringLength(256, MinimumLength = 1)]
  string FriendlyName);

public record CreateApiKeyResponseDto(
  ApiKeyDto ApiKey,
  string PlainTextKey);

public record UpdateApiKeyRequestDto(
  [Required]
  [StringLength(256, MinimumLength = 1)]
  string FriendlyName);
