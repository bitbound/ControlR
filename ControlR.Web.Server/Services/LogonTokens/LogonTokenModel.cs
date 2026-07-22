
namespace ControlR.Web.Server.Services.LogonTokens;

public class LogonTokenModel
{
  private int _consumed;

  public required DateTimeOffset CreatedAt { get; set; }
  public required Guid DeviceId { get; set; }
  public required DateTimeOffset ExpiresAt { get; set; }
  public bool IsConsumed => Volatile.Read(ref _consumed) == 1;
  public string? SessionCorrelationId { get; set; }
  public required Guid TenantId { get; set; }
  public required string Token { get; set; }
  public string? UserCorrelationId { get; set; }
  public required Guid UserId { get; set; }

  /// <summary>
  /// Atomically transitions the token to the consumed state. Returns <see langword="true"/>
  /// for exactly one caller — the one that observes the token as unconsumed and claims it.
  /// Every other caller, whether concurrent or subsequent, receives <see langword="false"/>.
  /// This is the single-use gate and requires no external locking.
  /// </summary>
  public bool TryConsume()
  {
    return Interlocked.CompareExchange(ref _consumed, 1, 0) == 0;
  }
}
