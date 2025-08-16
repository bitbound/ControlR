using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace ControlR.DesktopClient;

internal static class Localization
{
  private static readonly JsonSerializerOptions _jsonOptions = new()
  {
    AllowTrailingCommas = true,
    PropertyNameCaseInsensitive = true,
    ReadCommentHandling = JsonCommentHandling.Skip
  };

  private static readonly Dictionary<string, string> _localizationStrings = GetLocalizationStrings();
  public static string AccessibilityPermissionDescription => GetString();
  public static string Accessibility => GetString();
  public static string CancelText => GetString();
  public static string EnterCodePlaceholder => GetString();
  public static string GetSupportDescription => GetString();
  public static string GetTechSupportTitle => GetString();
  public static string Granted => GetString();
  public static string MacOsPermissions => GetString();
  public static string ManagedDeviceMessage => GetString();
  public static string ManagedDeviceTitle => GetString();
  public static string NoText => GetString();
  public static string NotGranted => GetString();
  public static string OkText => GetString();
  public static string OpenSettings => GetString();
  public static string ScreenCapturePermissionDescription => GetString();
  public static string ScreenRecording => GetString();
  public static string ShareScreenSecurityWarning => GetString();
  public static string Status => GetString();
  public static string SubmitText => GetString();
  public static string YesText => GetString();

  private static ILogger<Program> Logger { get; } = App.ServiceProvider.GetRequiredService<ILogger<Program>>();

  private static Dictionary<string, string> GetLocalizationStrings()
  {
    var cultureName = CultureInfo.CurrentCulture.Name;

    var assembly = Assembly.GetExecutingAssembly();
    var resourceNames = assembly.GetManifestResourceNames();

    if (resourceNames.FirstOrDefault(x => x.EndsWith($"{cultureName}.json")) is not { } fileName)
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

    if (Design.IsDesignMode)
    {
      // Throw in design mode so we catch errors.
      throw new InvalidOperationException($"Localization key '{key}' not found.");
    }

    // In runtime, log the error and return a placeholder. We don't want to crash the app.
    Logger.LogError("Localization key '{KeyName}' not found.", key);
    return $"Localization key '{key}' not found.";
  }
}