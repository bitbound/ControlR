using System.Security.Cryptography;
using System.Text;

namespace ControlR.Libraries.Api.Contracts.Dtos.RemoteControlDtos;

/// <summary>
/// The viewer command kinds a responder must accept for one session, sealed by the server at join
/// time over the authenticated control channel. The responder gate is the authority on inbound
/// viewer commands. A relay drop is only an optimization, and a relay allow is never a grant.
/// A null policy on the wire means the sender predates command policies.
/// </summary>
[MessagePackObject(keyAsPropertyName: true)]
public record RemoteControlCommandPolicyDto(
  Guid SessionId,
  int PolicyVersion,
  string PolicyHash,
  IReadOnlyList<RemoteControlViewerDtoType> AllowedDtoTypes)
{
  /// <summary>
  /// Version of the policy contract itself, mixed into the seal so a future format change cannot
  /// be mistaken for an old-but-valid set.
  /// </summary>
  public const int CurrentPolicyVersion = 1;

  public static RemoteControlCommandPolicyDto Create(
    Guid sessionId,
    IEnumerable<RemoteControlViewerDtoType> allowedDtoTypes)
  {
    var frozen = allowedDtoTypes
      .Distinct()
      .OrderBy(x => (int)x)
      .ToArray();

    return new RemoteControlCommandPolicyDto(
      sessionId,
      CurrentPolicyVersion,
      ComputeHash(sessionId, CurrentPolicyVersion, frozen),
      frozen);
  }

  /// <summary>
  /// True when the policy was sealed by the server for this exact session and its allowed set has
  /// not been altered in transit. Callers treat false as fail-closed.
  /// </summary>
  public bool IsBoundTo(Guid sessionId) =>
    SessionId == sessionId &&
    !string.IsNullOrEmpty(PolicyHash) &&
    PolicyHash == ComputeHash(SessionId, PolicyVersion, AllowedDtoTypes ?? []);

  private static string ComputeHash(
    Guid sessionId,
    int policyVersion,
    IReadOnlyList<RemoteControlViewerDtoType> allowedDtoTypes)
  {
    var canonical =
      $"{sessionId:N}|{policyVersion}|{string.Join(',', allowedDtoTypes.Select(x => (int)x))}";
    var digest = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
    return Convert.ToHexString(digest, 0, 8).ToLowerInvariant();
  }
}
