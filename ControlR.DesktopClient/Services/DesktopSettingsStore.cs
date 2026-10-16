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
    Current = Read(_settingsFilePath, fileSystem, logger);
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
  private static DesktopSettings Read(string settingsFilePath, IFileSystem fileSystem, ILogger logger)
  {
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
