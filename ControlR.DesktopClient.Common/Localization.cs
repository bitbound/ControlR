using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace ControlR.DesktopClient.Common;

public static class Localization
{

  private static readonly JsonSerializerOptions _jsonOptions = new()
  {
    AllowTrailingCommas = true,
    PropertyNameCaseInsensitive = true,
    ReadCommentHandling = JsonCommentHandling.Skip
  };
  private static string _currentCulture = CultureInfo.CurrentCulture.Name;

  private static Dictionary<string, string> _localizationStrings = GetLocalizationStrings();

  public static string ADeviceAdministrator => GetString();
  public static string Accessibility => GetString();
  public static string AccessibilityPermissionDescription => GetString();
  public static string CancelText => GetString();
  public static string EnterCodePlaceholder => GetString();
  public static string GetSupportDescription => GetString();
  public static string GetTechSupportTitle => GetString();
  public static string Granted => GetString();
  public static string MacOsPermissions => GetString();
  public static string ManagedDeviceMessage => GetString();
  public static string ManagedDeviceTitle => GetString();
  public static string NewChatMessageToastTitle => GetString();
  public static string NewChatMessageToastMessage => GetString();
  public static string NoText => GetString();
  public static string NotGranted => GetString();
  public static string NotificationPermissionDescription => GetString();
  public static string Notifications => GetString();
  public static string OkText => GetString();
  public static string OpenSettings => GetString();
  public static string RemoteControlSessionToastMessage => GetString();
  public static string RemoteControlSessionToastTitle => GetString();
  public static string ScreenCapturePermissionDescription => GetString();
  public static string ScreenRecording => GetString();
  public static string ShareScreenSecurityWarning => GetString();
  public static string Status => GetString();
  public static string SubmitText => GetString();
  public static string YesText => GetString();

  public static void SetCulture(string culture)
  {
    _currentCulture = culture;
    _localizationStrings = GetLocalizationStrings();
  }

  private static Dictionary<string, string> GetLocalizationStrings()
  {
    var assembly = typeof(Localization).Assembly;
    var resourceNames = assembly.GetManifestResourceNames();

    if (resourceNames.FirstOrDefault(x => x.EndsWith($"{_currentCulture}.json")) is not { } fileName)
    {
      fileName = $"{assembly.GetName().Name}.Resources.Strings.en-US.json";
    }

    using var resourceStream = assembly.GetManifestResourceStream(fileName)
                               ?? throw new InvalidOperationException("Unable to find localization file.");

    using var reader = new StreamReader(resourceStream);
    var content = reader.ReadToEnd();
    return JsonSerializer.Deserialize<Dictionary<string, string>>(content, _jsonOptions)
           ?? throw new InvalidOperationException("Unable to deserialize localization file.");
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