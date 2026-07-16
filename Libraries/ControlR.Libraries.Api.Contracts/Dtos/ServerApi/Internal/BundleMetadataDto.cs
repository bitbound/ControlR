using ControlR.Libraries.Api.Contracts.Enums;

namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

// This DTO is deserialized by agents running the previous release. The server
// preserves the pre-existing /api/agent-update route so outdated agents can
// still poll for bundle metadata and self-update. Backwards-compatible shape
// is required: do not remove or rename fields, and do not change the response
// type's wire name.
public class BundleMetadataDto
{
  public required string BundleDownloadUrl { get; set; }
  public required string BundleSha256 { get; set; }
  public required string InstallerDownloadUrl { get; set; }
  public required string InstallerSha256 { get; set; }
  public required RuntimeId Runtime { get; set; }
  public required Version Version { get; set; }
}
