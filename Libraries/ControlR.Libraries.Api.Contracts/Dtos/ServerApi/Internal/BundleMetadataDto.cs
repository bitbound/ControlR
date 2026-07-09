using ControlR.Libraries.Api.Contracts.Enums;

namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

public class BundleMetadataDto
{
  public required string BundleDownloadUrl { get; set; }
  public required string BundleSha256 { get; set; }
  public required string InstallerDownloadUrl { get; set; }
  public required string InstallerSha256 { get; set; }
  public required RuntimeId Runtime { get; set; }
  public required Version Version { get; set; }
}
