using System.Text.Json;
using System.Text.Json.Serialization;
using ControlR.Libraries.Branding;
using Microsoft.Extensions.Logging;

namespace ControlR.Assist.Services;

public class AssistAppSettings
{
  public string? ServerUrl { get; set; }

  // Nullable because a settings file written by another build can carry a null history, and the
  // deserializer would overwrite the initializer with it.
  public List<string>? ServerUrlHistory { get; set; } = [];
}

public interface IAssistSettingsService
{
  AssistAppSettings GetSettings();
  Task SaveAsync(AssistAppSettings settings, CancellationToken cancellationToken);
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

  public static void RememberUrl(AssistAppSettings settings, string url)
  {
    settings.ServerUrl = url;

    var history = settings.ServerUrlHistory ??= [];

    history.Remove(url);
    history.Insert(0, url);

    if (history.Count > MaxHistoryEntries)
    {
      history.RemoveRange(MaxHistoryEntries, history.Count - MaxHistoryEntries);
    }
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

  public Task SaveAsync(AssistAppSettings settings, CancellationToken cancellationToken)
  {
    try
    {
      var json = JsonSerializer.Serialize(settings, AssistSettingsJsonContext.Default.AssistAppSettings);

      // The write is synchronous because the lock scope cannot span an await.
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

    return Task.CompletedTask;
  }
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(AssistAppSettings))]
internal partial class AssistSettingsJsonContext : JsonSerializerContext;
