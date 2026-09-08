using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using ControlR.Web.Server.Authz.Permissions;

namespace ControlR.Web.Server.Services.Authorization;

/// <summary>
/// Creates <see cref="AuthorizationChangeLog"/> entries with typed before/after snapshots.
/// The returned entity is NOT saved by the factory: callers add it to their own DbContext so
/// it commits in the same transaction as the mutation it records.
/// </summary>
public interface IAuthorizationChangeLogFactory
{
  AuthorizationChangeLog Create(
    string actionType,
    PrincipalDescriptor? actor,
    string targetType,
    Guid? targetId,
    Guid? owningTenantId,
    object? before = null,
    object? after = null);
}

public class AuthorizationChangeLogFactory(IHttpContextAccessor httpContextAccessor) : IAuthorizationChangeLogFactory
{
  private static readonly JsonSerializerOptions _serializerOptions = new()
  {
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    Converters = { new JsonStringEnumConverter() }
  };

  private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;

  public AuthorizationChangeLog Create(
    string actionType,
    PrincipalDescriptor? actor,
    string targetType,
    Guid? targetId,
    Guid? owningTenantId,
    object? before = null,
    object? after = null)
  {
    return new AuthorizationChangeLog
    {
      ActionType = actionType,
      ActorPrincipalType = actor?.PrincipalType.ToAuthorizationChangeLogActorType() ?? AuthorizationChangeLogActorTypes.System,
      ActorPrincipalId = NormalizeEmptyGuid(actor?.PrincipalId),
      TargetType = targetType,
      TargetId = NormalizeEmptyGuid(targetId),
      OwningTenantId = owningTenantId,
      BeforeJson = before is not null ? JsonSerializer.Serialize(before, _serializerOptions) : null,
      AfterJson = after is not null ? JsonSerializer.Serialize(after, _serializerOptions) : null,
      IpAddress = ResolveIpAddress(),
      CorrelationId = ResolveCorrelationId()
    };
  }

  /// <summary>
  /// Normalizes a <see cref="Guid.Empty"/> to <see langword="null"/> so an unresolved
  /// (e.g. pre-save) entity ID can never be written to the audit log as a literal
  /// <c>00000000-0000-0000-0000-000000000000</c>.
  /// </summary>
  private static Guid? NormalizeEmptyGuid(Guid? value) =>
    value is { } nonNull && nonNull != Guid.Empty ? nonNull : null;

  /// <summary>
  /// Returns the current W3C trace id (stable across the request chain), or null for
  /// background services without ambient activity.
  /// </summary>
  private static string? ResolveCorrelationId()
  {
    var traceId = Activity.Current?.TraceId;
    return traceId is { } id && id != default ? id.ToString() : null;
  }

  /// <summary>
  /// Resolves the caller's IP address from the ambient HTTP context. Background services and
  /// other non-HTTP callers have no <see cref="HttpContext"/> and get <see langword="null"/>.
  /// </summary>
  private string? ResolveIpAddress()
  {
    var remoteIp = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress;
    if (remoteIp is null)
    {
      return null;
    }

    var ipString = remoteIp.IsIPv4MappedToIPv6
      ? remoteIp.MapToIPv4().ToString()
      : remoteIp.ToString();

    return ipString.Length <= 64 ? ipString : ipString[..64];
  }
}
