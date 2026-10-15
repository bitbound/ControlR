using System.Text.Json;
using ControlR.Libraries.Shared.Services.FileSystem;

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
public class DesktopSettingsStore(string settingsFilePath, IFileSystem fileSystem) : IDesktopSettingsStore
{
  private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

  public DesktopSettings Current { get; private set; } = Read(settingsFilePath, fileSystem);

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

  private static DesktopSettings Read(string settingsFilePath, IFileSystem fileSystem)
  {
    if (!fileSystem.FileExists(settingsFilePath))
    {
      return new DesktopSettings();
    }

    return JsonSerializer.Deserialize<DesktopSettings>(fileSystem.ReadAllText(settingsFilePath))
           ?? new DesktopSettings();
  }

  private void Save()
  {
    fileSystem.CreateDirectory(Path.GetDirectoryName(settingsFilePath)!);
    fileSystem.WriteAllText(settingsFilePath, JsonSerializer.Serialize(Current, _jsonOptions));
  }
}

public record DesktopSettings
{
  public AvaloniaThemeMode Theme { get; init; } = AvaloniaThemeMode.Dark;
  public string? Language { get; init; }
}
