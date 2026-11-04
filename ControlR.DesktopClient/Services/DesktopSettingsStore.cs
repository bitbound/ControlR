using System.Text.Json;
using ControlR.Libraries.Branding;
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
  // The agent keeps its signing key and bundle hash in this same directory when this client runs as root,
  // and relocates them itself.
  private static readonly HashSet<string> _filesOwnedByTheAgent = new(StringComparer.Ordinal)
  {
    "appsettings.json",
    BrandingConstants.BundleHashFileName
  };

  private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

  private readonly IFileSystem _fileSystem;
  private readonly ILogger<DesktopSettingsStore> _logger;
  private readonly IDesktopPathProvider _pathProvider;
  private readonly string _settingsFilePath;

  public DesktopSettingsStore(
    IDesktopPathProvider pathProvider,
    IFileSystem fileSystem,
    ILogger<DesktopSettingsStore> logger)
  {
    _pathProvider = pathProvider;
    _settingsFilePath = pathProvider.GetSettingsPath();
    _fileSystem = fileSystem;
    _logger = logger;
    Current = Read();
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
  private DesktopSettings Read()
  {
    // TODO: Temporary migration code for v0.31.x
    if (!_fileSystem.FileExists(_settingsFilePath))
    {
      TryCarryLegacySettings();
    }

    try
    {
      if (!_fileSystem.FileExists(_settingsFilePath))
      {
        return new DesktopSettings();
      }

      return JsonSerializer.Deserialize<DesktopSettings>(_fileSystem.ReadAllText(_settingsFilePath))
             ?? new DesktopSettings();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Ignoring unusable settings file {SettingsPath}", _settingsFilePath);
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

  /// <summary>
  /// TODO: Temporary migration code for v0.31.x
  /// Moves this client's files from the settings directory it used before its path grew a publisher
  /// segment. This client is the only writer of those files, so it is also the only process that can put
  /// them at the new path with the owners they already had.
  /// </summary>
  private void TryCarryLegacySettings()
  {
    var legacySettingsDirectoryPath = _pathProvider.GetLegacySettingsDirectory();

    try
    {
      if (!_fileSystem.DirectoryExists(legacySettingsDirectoryPath))
      {
        return;
      }

      // Everything else at the top of that directory is this client's. Its subdirectories hold logs, and
      // old logs stay where they are.
      var legacyFilePaths = _fileSystem
        .GetFiles(legacySettingsDirectoryPath)
        .Where(filePath => !_filesOwnedByTheAgent.Contains(Path.GetFileName(filePath)))
        .ToList();

      if (legacyFilePaths.Count == 0)
      {
        return;
      }

      var settingsDirectoryPath = Path.GetDirectoryName(_settingsFilePath)!;
      _fileSystem.CreateDirectory(settingsDirectoryPath);

      foreach (var legacyFilePath in legacyFilePaths)
      {
        var fileName = Path.GetFileName(legacyFilePath);
        var destinationPath = Path.Combine(settingsDirectoryPath, fileName);

        // A file already at the new path is the newer copy this client wrote itself.
        if (_fileSystem.FileExists(destinationPath))
        {
          continue;
        }

        _fileSystem.MoveFile(legacyFilePath, destinationPath, overwrite: false);
        _logger.LogInformation(
          "Moved {FileName} from {LegacySettingsDirectory}.",
          fileName,
          legacySettingsDirectoryPath);
      }
    }
    catch (Exception ex)
    {
      // Choosing the language and theme again is a smaller cost than not starting.
      _logger.LogWarning(ex, "Unable to carry the settings from {LegacySettingsDirectory}.", legacySettingsDirectoryPath);
    }
  }
}

public record DesktopSettings
{
  public AvaloniaThemeMode Theme { get; init; } = AvaloniaThemeMode.Dark;
  public string? Language { get; init; }
}
