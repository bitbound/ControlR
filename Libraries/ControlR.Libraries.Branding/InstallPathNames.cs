namespace ControlR.Libraries.Branding;

/// <summary>
/// The vendor-scoped segments that an install, settings, or log path is built from. The publisher is a
/// path segment only. File names, service names, registry keys, and pipe names stay brand-key derived in
/// <see cref="BrandNames"/>, so an install keeps its OS identity when its files move under a vendor
/// directory and a move can be done by repointing rather than renaming.
/// </summary>
public static class InstallPathNames
{
  /// <summary>
  /// The publisher and brand directories, cased as the roots that use them do today. A null or blank
  /// publisher names the vendorless path, which is the shape installs used before the publisher became a
  /// directory. Callers pass null only when addressing an install that predates the vendor directory.
  /// </summary>
  public static string[] Directories(string? publisherName, string brandName)
  {
    var brandDirectory = new BrandNames(brandName).BrandKey;
    var publisherDirectory = PublisherDirectory(publisherName);

    return publisherDirectory is null
      ? [brandDirectory]
      : [publisherDirectory, brandDirectory];
  }

  /// <summary>
  /// The hidden home directory segments, which put the leading dot on the publisher because that segment
  /// is the one a user sees in their profile. With no publisher the dot stays on the brand, as it does in
  /// the vendorless path.
  /// </summary>
  public static string[] HiddenHomeDirectories(string? publisherName, string brandName)
  {
    var directories = LowercaseDirectories(publisherName, brandName);
    directories[0] = $".{directories[0]}";
    return directories;
  }

  /// <summary>
  /// The publisher and brand directories for the Unix roots that are lowercase today, which are the
  /// config, log, and hidden-home roots.
  /// </summary>
  public static string[] LowercaseDirectories(string? publisherName, string brandName)
  {
    var brandNames = new BrandNames(brandName);
    var publisherDirectory = PublisherDirectory(publisherName)?.ToLowerInvariant();

    return publisherDirectory is null
      ? [brandNames.UnixBrandKey]
      : [publisherDirectory, brandNames.UnixBrandKey];
  }

  private static string? PublisherDirectory(string? publisherName)
  {
    return string.IsNullOrWhiteSpace(publisherName)
      ? null
      : BrandNames.SanitizeBrandKey(publisherName);
  }
}
