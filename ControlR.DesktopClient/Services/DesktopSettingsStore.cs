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
    Current = Read(_settingsFilePath, pathProvider.GetLegacySettingsDirectory(), fileSystem, logger);
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
    string legacySettingsDirectoryPath,
    IFileSystem fileSystem,
    ILogger logger)
  {
    // TODO: Temporary migration code for v0.31.x
    if (!fileSystem.FileExists(settingsFilePath))
    {
      TryCarryLegacySettings(settingsFilePath, legacySettingsDirectoryPath, fileSystem, logger);
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
  /// Moves the files this client wrote before its settings directory grew a publisher segment. This
  /// client is the only writer of that directory, so it is also the only process that can put those
  /// files at the new path with the owners they already had.
  /// </summary>
  private static void TryCarryLegacySettings(
    string settingsFilePath,
    string legacySettingsDirectoryPath,
    IFileSystem fileSystem,
    ILogger logger)
  {
    try
    {
      if (!fileSystem.DirectoryExists(legacySettingsDirectoryPath))
      {
        return;
      }

      // Everything at the top of that directory belongs to this client, so all of it comes along rather
      // than one file name at a time. Its subdirectories hold logs, and old logs stay where they are.
      var settingsDirectoryPath = Path.GetDirectoryName(settingsFilePath)!;
      fileSystem.CreateDirectory(settingsDirectoryPath);

      foreach (var legacyFilePath in fileSystem.GetFiles(legacySettingsDirectoryPath))
      {
        var destinationPath = Path.Combine(settingsDirectoryPath, Path.GetFileName(legacyFilePath));

        // A file already at the new path is the newer copy this client wrote itself.
        if (fileSystem.FileExists(destinationPath))
        {
          continue;
        }

        fileSystem.MoveFile(legacyFilePath, destinationPath, overwrite: false);
        logger.LogInformation(
          "Moved {FileName} from {LegacySettingsDirectory}, the settings directory used before the publisher directory.",
          Path.GetFileName(legacyFilePath),
          legacySettingsDirectoryPath);
      }
    }
    catch (Exception ex)
    {
      // Choosing the language and theme again is a smaller cost than not starting.
      logger.LogWarning(ex, "Unable to carry the settings from {LegacySettingsDirectory}.", legacySettingsDirectoryPath);
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
