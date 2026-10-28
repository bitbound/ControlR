using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using ControlR.Libraries.Branding;

namespace ControlR.Assist;

/// <summary>
/// Same strategy as the desktop client: one embedded JSON file per culture, resolved by member name.
/// </summary>
public static class Localization
{
  private const string BrandNameToken = "{brand_name}";
  private const string DefaultCulture = "en-US";
  private const string LanguageDisplayNameKey = nameof(LanguageDisplayName);
  private const string StringsResourceMarker = ".Resources.Strings.";
  private const string StringsSuffix = ".json";

  private static string _currentCulture = CultureInfo.CurrentCulture.Name;
  private static Dictionary<string, string> _localizationStrings = GetLocalizationStrings(_currentCulture);

  public static string AcceptButtonText => GetString();
  public static string AccessCodeCaption => GetString();
  public static string ConnectButtonText => GetString();
  public static string ConsentPromptFormat => GetString();
  public static string DeclineButtonText => GetString();
  public static string DisconnectButtonText => GetString();
  public static string EndSessionButtonText => GetString();
  public static string HomeTagline => GetString();
  public static string LanguageDisplayName => GetString();
  public static string NewCodeButtonText => GetString();
  public static string ServerUrlWatermark => GetString();
  public static string SessionActiveCaption => GetString();
  public static string StatusConnectedToViewer => GetString();
  public static string StatusConnecting => GetString();
  public static string StatusConsentResponseFailed => GetString();
  public static string StatusInvalidUrl => GetString();
  public static string StatusReconnected => GetString();
  public static string StatusSessionCreateFailed => GetString();
  public static string StatusSessionEnded => GetString();
  public static string StatusSessionStartFailed => GetString();
  public static string StatusUnreachable => GetString();
  public static string UnknownViewerLabel => GetString();
  public static string ViewNotFoundMessage => GetString();
  public static string ViewOnlyButtonText => GetString();

  /// <summary>
  /// The embedded cultures, each labeled in its own language.
  /// </summary>
  public static IReadOnlyList<LocalizationLanguage> GetAvailableLanguages()
  {
    var assembly = typeof(Localization).Assembly;

    return GetCultures(assembly.GetManifestResourceNames())
      .Select(culture => new LocalizationLanguage(culture, GetLocalizationStrings(culture)[LanguageDisplayNameKey]))
      .OrderBy(x => x.DisplayName)
      .ToList();
  }

  public static void SetCulture(string culture)
  {
    _currentCulture = culture;
    _localizationStrings = GetLocalizationStrings(_currentCulture);
  }

  // Manifest names come from RootNamespace, which stays the project name in a white-label build even
  // though the assembly is renamed. Only the tail after the strings directory is dependable.
  internal static List<string> GetCultures(IEnumerable<string> resourcePaths)
  {
    return resourcePaths
      .Select(path => (path, markerIndex: path.IndexOf(StringsResourceMarker, StringComparison.Ordinal)))
      .Where(x => x.markerIndex >= 0 && x.path.EndsWith(StringsSuffix, StringComparison.Ordinal))
      .Select(x => x.path[(x.markerIndex + StringsResourceMarker.Length)..^StringsSuffix.Length])
      .Where(culture => culture.Length > 0)
      .ToList();
  }

  private static string? FindResourcePath(IEnumerable<string> resourcePaths, string culture)
  {
    // An empty culture name would match every strings file, so require an exact one.
    if (string.IsNullOrEmpty(culture))
    {
      return null;
    }

    var suffix = $"{culture}{StringsSuffix}";

    return resourcePaths.FirstOrDefault(x =>
      x.EndsWith(suffix, StringComparison.Ordinal) &&
      x.Contains(StringsResourceMarker, StringComparison.Ordinal));
  }

  private static Dictionary<string, string> GetLocalizationStrings(string culture)
  {
    var assembly = typeof(Localization).Assembly;
    var resourcePaths = assembly.GetManifestResourceNames();
    var fileName =
      FindResourcePath(resourcePaths, culture)
      ?? FindResourcePath(resourcePaths, DefaultCulture)
      ?? throw new InvalidOperationException("Unable to find localization file.");

    using var resourceStream = assembly.GetManifestResourceStream(fileName)
                               ?? throw new InvalidOperationException("Unable to find localization file.");

    using var reader = new StreamReader(resourceStream);
    var content = reader.ReadToEnd();
    var strings = JsonSerializer.Deserialize(content, StringsJsonContext.Default.DictionaryStringString)
                  ?? throw new InvalidOperationException("Unable to deserialize localization file.");

    return strings.ToDictionary(x => x.Key, x => x.Value.Replace(BrandNameToken, BrandingConstants.BrandName));
  }

  private static string GetString([CallerMemberName] string key = "")
  {
    if (_localizationStrings.TryGetValue(key, out var value))
    {
      return value;
    }

    return $"Localization key '{key}' not found.";
  }
}

/// <summary>
/// The desktop client deserializes its strings file with reflection. Assist ships trimmed, so the
/// same deserialization has to go through a source-generated type instead.
/// </summary>
[JsonSourceGenerationOptions(
  AllowTrailingCommas = true,
  PropertyNameCaseInsensitive = true,
  ReadCommentHandling = JsonCommentHandling.Skip)]
[JsonSerializable(typeof(Dictionary<string, string>))]
internal partial class StringsJsonContext : JsonSerializerContext;

public record LocalizationLanguage(string Culture, string DisplayName);
