using System.Diagnostics.CodeAnalysis;

namespace ControlR.Web.Server.Services.LogonTokens;

public class LogonTokenValidationResult
{
  public IReadOnlyList<int>? AllowedDesktopSessionIds { get; set; }
  public string? ErrorMessage { get; set; }
  public DateTimeOffset? ExpiresAt { get; set; }

  [MemberNotNullWhen(true, nameof(UserId), nameof(TenantId), nameof(TokenId), nameof(ExpiresAt))]
  public bool IsValid { get; set; }
  public string? SessionCorrelationId { get; set; }

  /// <summary>
  /// Absolute cap, in minutes, applied to the cookie session minted on redemption.
  /// </summary>
  public int SessionExpirationMinutes { get; set; } = DtoLimits.SessionExpirationMinutesDefault;
  public Guid? TenantId { get; set; }
  public Guid? TokenId { get; set; }
  public Guid? UserId { get; set; }

  public static LogonTokenValidationResult Failure(string errorMessage)
  {
    return new LogonTokenValidationResult
    {
      IsValid = false,
      ErrorMessage = errorMessage
    };
  }

  public static LogonTokenValidationResult Success(
    Guid tokenId,
    Guid userId,
    Guid tenantId,
    DateTimeOffset expiresAt,
    string? sessionCorrelationId = null,
    IReadOnlyList<int>? allowedDesktopSessionIds = null,
    int sessionExpirationMinutes = DtoLimits.SessionExpirationMinutesDefault)
  {
    return new LogonTokenValidationResult
    {
      IsValid = true,
      TokenId = tokenId,
      UserId = userId,
      TenantId = tenantId,
      ExpiresAt = expiresAt,
      SessionCorrelationId = sessionCorrelationId,
      AllowedDesktopSessionIds = allowedDesktopSessionIds,
      SessionExpirationMinutes = sessionExpirationMinutes
    };
  }
}
