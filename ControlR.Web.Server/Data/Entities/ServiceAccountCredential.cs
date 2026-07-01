using System.ComponentModel.DataAnnotations;
using ControlR.Web.Server.Data.Entities.Bases;

namespace ControlR.Web.Server.Data.Entities;

/// <summary>
/// A credential (secret) belonging to a <see cref="ServiceAccount"/>. The credential
/// <see cref="EntityBase.Id"/> (Guid) is the lookup key for the <c>x-api-key</c> header
/// (<c>{hex_id}:{plaintext_secret}</c>), matching the existing PAT header format.
/// </summary>
public class ServiceAccountCredential : EntityBase
{
  public DateTimeOffset? ExpiresAt { get; set; }
  [StringLength(256)]
  public required string HashedSecret { get; set; }
  public DateTimeOffset? LastUsedAt { get; set; }
  [StringLength(100)]
  public required string Name { get; set; }
  public DateTimeOffset? RevokedAt { get; set; }
  public ServiceAccount ServiceAccount { get; set; } = null!;
  public Guid ServiceAccountId { get; set; }
}