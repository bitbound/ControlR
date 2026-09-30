using System.ComponentModel.DataAnnotations;
using ControlR.Web.Server.Data.Entities.Bases;

namespace ControlR.Web.Server.Data.Entities;

public class LogonToken : TenantEntityBase
{
  public IReadOnlyList<int>? AllowedDesktopSessionIds { get; set; }
  public Device? Device { get; set; }
  public Guid DeviceId { get; set; }
  public DateTimeOffset ExpiresAt { get; set; }
  public bool IsConsumed { get; set; }

  [StringLength(32)]
  public string? Prefix { get; set; }

  [StringLength(DtoLimits.SessionCorrelationIdMaxLength)]
  public string? SessionCorrelationId { get; set; }

  /// <summary>
  /// Fixed duration, in minutes, of the cookie session minted when the token is redeemed.
  /// The session is not activity-refreshed, so it ends this long after redemption.
  /// </summary>
  public int SessionExpirationMinutes { get; set; } = DtoLimits.SessionExpirationMinutesDefault;

  [StringLength(256)]
  public required string Token { get; set; }
  public AppUser? User { get; set; }

  [StringLength(DtoLimits.UserCorrelationIdMaxLength)]
  public string? UserCorrelationId { get; set; }
  public Guid UserId { get; set; }
}
