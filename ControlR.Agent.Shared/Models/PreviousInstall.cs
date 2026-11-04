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
/// <param name="Publisher">
/// Publisher of the install being replaced, which is the vendor directory its paths sit under. Null is
/// the install from before the publisher became a directory, which is what an agent that predates it
/// describes when it names no publisher at all.
/// </param>
public sealed record PreviousInstall(string BrandName, string? InstanceId, string? Publisher)
{
  /// <summary>
  /// Resolves the install being replaced. A brand name that was not supplied describes an install
  /// identical to this one on that axis, so it is filled from this installer, and an instance id that was
  /// not supplied means the replaced install used the default one. The publisher is not filled at all:
  /// an agent that predates the vendor directory has no publisher to name, and the install it is replacing
  /// lives at the path without one.
  /// </summary>
  /// <returns>
  /// The install to replace, or null when the arguments name no other install. That is the case when
  /// none were supplied at all, and when they describe this installer's own install, where replacing
  /// would remove what was just written.
  /// </returns>
  public static PreviousInstall? Resolve(
    string? previousPublisher,
    string? previousBrandName,
    string? previousInstanceId,
    string installerPublisher,
    string installerBrandName,
    string? installerInstanceId)
  {
    var suppliedPublisher = string.IsNullOrWhiteSpace(previousPublisher) ? null : previousPublisher;
    var suppliedBrandName = string.IsNullOrWhiteSpace(previousBrandName) ? null : previousBrandName;
    var suppliedInstanceId = string.IsNullOrWhiteSpace(previousInstanceId) ? null : previousInstanceId;

    if (suppliedPublisher is null && suppliedBrandName is null && suppliedInstanceId is null)
    {
      return null;
    }

    var brandName = suppliedBrandName ?? installerBrandName;

    if (BrandNames.AreSameInstall(brandName, installerBrandName) &&
        InstallPathNames.AreSameVendor(suppliedPublisher, installerPublisher) &&
        string.Equals(
          GetEffectiveInstanceId(suppliedInstanceId),
          GetEffectiveInstanceId(installerInstanceId),
          StringComparison.Ordinal))
    {
      return null;
    }

    return new PreviousInstall(brandName, suppliedInstanceId, suppliedPublisher);
  }

  private static string GetEffectiveInstanceId(string? instanceId)
  {
    return string.IsNullOrWhiteSpace(instanceId) ? AppConstants.DefaultInstanceId : instanceId;
  }
}
