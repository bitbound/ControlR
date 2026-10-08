namespace ControlR.Libraries.Branding;

public enum BrandMatch
{
  /// <summary>
  /// The remote bundle is the same brand as this build. An ordinary in-place update.
  /// </summary>
  SameBrand,

  /// <summary>
  /// The remote bundle is a different brand that declares this build's brand as one it may replace.
  /// A migration, which must move install identity rather than create a second agent.
  /// </summary>
  DeclaredPredecessor,

  /// <summary>
  /// Unrelated, malformed, or undecodable. Refuse.
  /// </summary>
  Unrelated
}

/// <summary>
/// Decides whether an inbound bundle may act on the running install.
/// </summary>
public static class BrandCompatibility
{
  /// <summary>
  /// Evaluates a remote bundle against the compiled-in brand of the current build.
  /// </summary>
  public static BrandMatch Evaluate(
    string? remoteBrandName,
    string? remotePublisher,
    IReadOnlyCollection<string>? remotePredecessorBrandNames)
  {
    return Evaluate(
      remoteBrandName,
      remotePublisher,
      remotePredecessorBrandNames,
      BrandingConstants.BrandName,
      BrandingConstants.Publisher,
      BrandingConstants.PredecessorBrandNames);
  }

  /// <summary>
  /// Pure overload taking both sides explicitly, so the comparison can be tested without a rebuild.
  /// </summary>
  public static BrandMatch Evaluate(
    string? remoteBrandName,
    string? remotePublisher,
    IReadOnlyCollection<string>? remotePredecessorBrandNames,
    string localBrandName,
    string localPublisher,
    IReadOnlyCollection<string>? localPredecessorBrandNames)
  {
    if (string.IsNullOrWhiteSpace(remoteBrandName) || string.IsNullOrWhiteSpace(localBrandName))
    {
      return BrandMatch.Unrelated;
    }

    var remoteKey = BrandingConstants.SanitizeBrandKey(remoteBrandName);
    var localKey = BrandingConstants.SanitizeBrandKey(localBrandName);

    // Distinct brand names can sanitize to one key, and that key is what addresses the install
    // directory, service name, and settings path. Treat a key collision as the same install, which
    // means the ordinary update rules including the publisher requirement apply.
    if (string.Equals(remoteKey, localKey, StringComparison.Ordinal))
    {
      return string.Equals(remotePublisher, localPublisher, StringComparison.Ordinal)
        ? BrandMatch.SameBrand
        : BrandMatch.Unrelated;
    }

    if (remotePredecessorBrandNames is null || remotePredecessorBrandNames.Count == 0)
    {
      return BrandMatch.Unrelated;
    }

    // The remote declares which brands it may replace. Publisher is not required to agree, because
    // a sponsor rebrand normally changes brand and publisher together.
    foreach (var predecessor in remotePredecessorBrandNames)
    {
      if (string.IsNullOrWhiteSpace(predecessor))
      {
        continue;
      }

      if (string.Equals(BrandingConstants.SanitizeBrandKey(predecessor), localKey, StringComparison.Ordinal))
      {
        return BrandMatch.DeclaredPredecessor;
      }
    }

    return BrandMatch.Unrelated;
  }
}
