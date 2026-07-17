namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

// DO NOT BREAK.
// This DTO is deserialized by agents running the previous release. 
// Backwards-compatible shape is required! Do not remove or rename 
// fields, and do not change the response type's wire name.
public class BundleMetadataDto
{
  public required string BundleDownloadUrl { get; set; }
  public required string BundleSha256 { get; set; }
  public required string InstallerDownloadUrl { get; set; }
  public required string InstallerSha256 { get; set; }
  public required RuntimeId Runtime { get; set; }
  public required Version Version { get; set; }
}
