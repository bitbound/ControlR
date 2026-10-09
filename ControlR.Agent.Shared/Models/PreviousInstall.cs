using ControlR.Libraries.Branding;

namespace ControlR.Agent.Shared.Models;

/// <summary>
/// The install a replacing installer has to stop and remove, resolved from the arguments the resident
/// install passed on the command line.
/// </summary>
/// <param name="BrandName">Brand name of the install being replaced.</param>
/// <param name="InstanceId">
/// Instance id of the install being replaced, or null when it used the default instance id. This is
/// not always the instance id this installer is going to.
/// </param>
public sealed record PreviousInstall(string BrandName, string? InstanceId)
{
  /// <summary>
  /// Resolves the install being replaced. An argument that was not supplied describes an install
  /// identical to this one on that axis, so it is filled from this installer, except for the instance
  /// id, where absence means the replaced install used the default instance id.
  /// </summary>
  /// <returns>
  /// The install to replace, or null when the arguments name no other install. That is the case when
  /// none were supplied at all, and when they describe this installer's own install, where replacing
  /// would remove what was just written.
  /// </returns>
  public static PreviousInstall? Resolve(
    string? previousBrandName,
    string? previousInstanceId,
    string installerBrandName,
    string? installerInstanceId)
  {
    var suppliedBrandName = string.IsNullOrWhiteSpace(previousBrandName) ? null : previousBrandName;
    var suppliedInstanceId = string.IsNullOrWhiteSpace(previousInstanceId) ? null : previousInstanceId;

    if (suppliedBrandName is null && suppliedInstanceId is null)
    {
      return null;
    }

    var brandName = suppliedBrandName ?? installerBrandName;

    if (BrandNames.AreSameInstall(brandName, installerBrandName) &&
        string.Equals(
          GetEffectiveInstanceId(suppliedInstanceId),
          GetEffectiveInstanceId(installerInstanceId),
          StringComparison.Ordinal))
    {
      return null;
    }

    return new PreviousInstall(brandName, suppliedInstanceId);
  }

  private static string GetEffectiveInstanceId(string? instanceId)
  {
    return string.IsNullOrWhiteSpace(instanceId) ? AppConstants.DefaultInstanceId : instanceId;
  }
}
