using System.Text.Json;
using ControlR.Libraries.Shared.Services.FileSystem;
using Microsoft.Extensions.Logging;

namespace ControlR.DesktopClient.Services;

public interface IDesktopSettingsStore
{
  DesktopSettings Current { get; }
  void SetLanguage(string language);
  void SetTheme(AvaloniaThemeMode theme);
}

/// <summary>
/// Theme and language choices, persisted to the settings file.
/// </summary>
public class DesktopSettingsStore : IDesktopSettingsStore
{
  private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

  private readonly IFileSystem _fileSystem;
  private readonly ILogger<DesktopSettingsStore> _logger;
  private readonly string _settingsFilePath;

  public DesktopSettingsStore(
    IDesktopPathProvider pathProvider,
    IFileSystem fileSystem,
    ILogger<DesktopSettingsStore> logger)
  {
    _settingsFilePath = pathProvider.GetSettingsPath();
    _fileSystem = fileSystem;
    _logger = logger;
    Current = Read(_settingsFilePath, pathProvider.GetLegacySettingsPath(), fileSystem, logger);
  }

  public DesktopSettings Current { get; private set; }

  public void SetLanguage(string language)
  {
    Current = Current with { Language = language };
    Save();
  }

  public void SetTheme(AvaloniaThemeMode theme)
  {
    Current = Current with { Theme = theme };
    Save();
  }

  // A file left half-written by an interrupted save, or edited by hand, must not keep the client
  // from starting. Theme and language both have other ways to be chosen, so start with defaults.
  private static DesktopSettings Read(
    string settingsFilePath,
    string legacySettingsFilePath,
    IFileSystem fileSystem,
    ILogger logger)
  {
    // TODO: Temporary migration code for v0.31.x
    if (!fileSystem.FileExists(settingsFilePath))
    {
      TryCarryLegacySettings(settingsFilePath, legacySettingsFilePath, fileSystem, logger);
    }

    try
    {
      if (!fileSystem.FileExists(settingsFilePath))
      {
        return new DesktopSettings();
      }

      return JsonSerializer.Deserialize<DesktopSettings>(fileSystem.ReadAllText(settingsFilePath))
             ?? new DesktopSettings();
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Ignoring unusable settings file {SettingsPath}", settingsFilePath);
      return new DesktopSettings();
    }
  }

  /// <summary>
  /// TODO: Temporary migration code for v0.31.x
  /// Moves the settings file the install left behind when its directory grew a publisher segment. This
  /// client is the only writer of that file, so it is also the only process that can put the copy at the
  /// new path with the same owner it had before.
  /// </summary>
  private static void TryCarryLegacySettings(
    string settingsFilePath,
    string legacySettingsFilePath,
    IFileSystem fileSystem,
    ILogger logger)
  {
    try
    {
      if (!fileSystem.FileExists(legacySettingsFilePath))
      {
        return;
      }

      fileSystem.CreateDirectory(Path.GetDirectoryName(settingsFilePath)!);
      fileSystem.MoveFile(legacySettingsFilePath, settingsFilePath, overwrite: false);
      logger.LogInformation(
        "Moved this client's settings from {LegacySettingsPath} to {SettingsPath}.",
        legacySettingsFilePath,
        settingsFilePath);
    }
    catch (Exception ex)
    {
      // Choosing the language and theme again is a smaller cost than not starting.
      logger.LogWarning(ex, "Unable to move the settings file from {LegacySettingsPath}.", legacySettingsFilePath);
    }
  }

  // Written through a temporary file so a failure part-way through leaves the previous
  // settings readable.
  private void Save()
  {
    try
    {
      _fileSystem.CreateDirectory(Path.GetDirectoryName(_settingsFilePath)!);

      var tempFilePath = $"{_settingsFilePath}.tmp";
      _fileSystem.WriteAllText(tempFilePath, JsonSerializer.Serialize(Current, _jsonOptions));
      _fileSystem.MoveFile(tempFilePath, _settingsFilePath, overwrite: true);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Unable to save settings to {SettingsPath}", _settingsFilePath);
    }
  }
}

public record DesktopSettings
{
  public AvaloniaThemeMode Theme { get; init; } = AvaloniaThemeMode.Dark;
  public string? Language { get; init; }
}
