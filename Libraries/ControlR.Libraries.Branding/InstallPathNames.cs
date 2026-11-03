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
  /// Whether two publisher names describe the same vendor directory. The comparison is on the sanitized
  /// key rather than the raw name, because the key is what names the directory, so two publishers that
  /// reduce to the same key describe one install.
  /// </summary>
  public static bool AreSameVendor(string? firstPublisherName, string? secondPublisherName)
  {
    return string.Equals(
      PublisherDirectory(firstPublisherName),
      PublisherDirectory(secondPublisherName),
      StringComparison.Ordinal);
  }

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


  /// <summary>
  /// The publisher and brand joined for the Unix roots that are lowercase today.
  /// </summary>
  public static string LowercaseUnixDirectoryTail(string? publisherName, string brandName)
  {
    return string.Join('/', LowercaseDirectories(publisherName, brandName));
  }

  /// <summary>
  /// The publisher and brand joined with a Unix separator, keeping the case each name was given. This is
  /// the form for roots that are cased today, such as the macOS Application Support directory.
  /// </summary>
  public static string UnixDirectoryTail(string? publisherName, string brandName)
  {
    return string.Join('/', Directories(publisherName, brandName));
  }

  /// <summary>
  /// The hidden home directory joined with a Unix separator, for a caller that has one path segment to
  /// combine rather than two.
  /// </summary>
  public static string UnixHiddenHomeTail(string? publisherName, string brandName)
  {
    return string.Join('/', HiddenHomeDirectories(publisherName, brandName));
  }

  /// <summary>
  /// The publisher and brand joined with a Windows separator. Joined by hand rather than through Path,
  /// because this string names a Windows path even when it is being built or asserted on a build host
  /// where Path does not treat a backslash as a separator.
  /// </summary>
  public static string WindowsDirectoryTail(string? publisherName, string brandName)
  {
    return string.Join('\\', Directories(publisherName, brandName));
  }

  private static string? PublisherDirectory(string? publisherName)
  {
    return string.IsNullOrWhiteSpace(publisherName)
      ? null
      : BrandNames.SanitizeBrandKey(publisherName);
  }
}
