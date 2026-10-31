using System.Text.Json;
using System.Text.Json.Serialization;
using ControlR.Libraries.Avalonia.Theming;
using ControlR.Libraries.Branding;
using Microsoft.Extensions.Logging;

namespace ControlR.Assist.Services;

public class AssistAppSettings
{
  public string? Language { get; set; }
  public string? ServerUrl { get; set; }

  // Nullable because a settings file written by another build can carry a null history, and the
  // deserializer would overwrite the initializer with it.
  public List<string>? ServerUrlHistory { get; set; } = [];
  public AvaloniaThemeMode Theme { get; set; } = AvaloniaThemeMode.System;
}

public interface IAssistSettingsService
{
  string? GetLanguage();
  AssistAppSettings GetSettings();
  AvaloniaThemeMode GetThemeMode();
  Task SaveAsync(AssistAppSettings settings, CancellationToken cancellationToken);
  void SetLanguage(string? language);
  void SetThemeMode(AvaloniaThemeMode mode);
}

public sealed class AssistSettingsService(ILogger<AssistSettingsService> logger) : IAssistSettingsService
{
  private const int MaxHistoryEntries = 10;

  private static readonly string _settingsDirectory = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
    BrandingConstants.BrandKey,
    "Assist");
  private static readonly string _settingsPath = Path.Combine(_settingsDirectory, "settings.json");

  private readonly ILogger<AssistSettingsService> _logger = logger;
  private readonly Lock _writeLock = new();

  /// <summary>
  /// Where the app writes its settings file and its log file.
  /// </summary>
  public static string AppDataDirectoryPath => _settingsDirectory;

  public static string LogFilePath => Path.Combine(_settingsDirectory, "assist.log");

  public static void ForgetUrl(AssistAppSettings settings, string url)
  {
    settings.ServerUrlHistory ??= [];
    _ = settings.ServerUrlHistory.Remove(url);

    if (string.Equals(settings.ServerUrl, url, StringComparison.OrdinalIgnoreCase))
    {
      settings.ServerUrl = null;
    }
  }

  /// <summary>
  /// Reads only the saved language, for the one caller that needs it before the app container and
  /// its logging exist. A file that cannot be read leaves the app in the system language.
  /// </summary>
  public static string? ReadSavedLanguage()
  {
    try
    {
      if (!File.Exists(_settingsPath))
      {
        return null;
      }

      var settings = JsonSerializer.Deserialize(
        File.ReadAllText(_settingsPath),
        AssistSettingsJsonContext.Default.AssistAppSettings);

      return string.IsNullOrWhiteSpace(settings?.Language) ? null : settings.Language;
    }
    catch (Exception)
    {
      return null;
    }
  }

  public static void RememberUrl(AssistAppSettings settings, string url)
  {
    settings.ServerUrl = url;

    var history = settings.ServerUrlHistory ??= [];

    history.RemoveAll(x => string.Equals(x, url, StringComparison.OrdinalIgnoreCase));
    history.Insert(0, url);

    if (history.Count > MaxHistoryEntries)
    {
      history.RemoveRange(MaxHistoryEntries, history.Count - MaxHistoryEntries);
    }
  }

  public string? GetLanguage()
  {
    var language = GetSettings().Language;
    return string.IsNullOrWhiteSpace(language) ? null : language;
  }

  public AssistAppSettings GetSettings()
  {
    try
    {
      if (!File.Exists(_settingsPath))
      {
        return new AssistAppSettings();
      }

      var json = File.ReadAllText(_settingsPath);
      var settings = JsonSerializer.Deserialize(json, AssistSettingsJsonContext.Default.AssistAppSettings)
        ?? new AssistAppSettings();

      settings.ServerUrlHistory ??= [];
      return settings;
    }
    catch (Exception ex)
    {
      _logger.LogWarning(ex, "Failed to read assist settings from {Path}.", _settingsPath);
      return new AssistAppSettings();
    }
  }

  public AvaloniaThemeMode GetThemeMode() => GetSettings().Theme;

  public Task SaveAsync(AssistAppSettings settings, CancellationToken cancellationToken)
  {
    Save(settings);
    return Task.CompletedTask;
  }

  public void SetLanguage(string? language)
  {
    var settings = GetSettings();
    settings.Language = language;
    Save(settings);
  }

  public void SetThemeMode(AvaloniaThemeMode mode)
  {
    var settings = GetSettings();
    settings.Theme = mode;
    Save(settings);
  }

  // The write is synchronous because the lock scope cannot span an await.
  private void Save(AssistAppSettings settings)
  {
    try
    {
      var json = JsonSerializer.Serialize(settings, AssistSettingsJsonContext.Default.AssistAppSettings);

      using (_writeLock.EnterScope())
      {
        Directory.CreateDirectory(_settingsDirectory);
        File.WriteAllText(_settingsPath, json);
      }
    }
    catch (Exception ex)
    {
      _logger.LogWarning(ex, "Failed to save assist settings to {Path}.", _settingsPath);
    }
  }
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(AssistAppSettings))]
internal partial class AssistSettingsJsonContext : JsonSerializerContext;
