using System.Text.Json;
using CommunityToolkit.Maui.Storage;

namespace ControlR.Viewer.Services;

internal interface ISettingsExporter
{
  Task<Result> ExportSettings();
  Task<Result> ImportSettings();
}

internal class SettingsExporter(
  ISettings settings,
  IFileSaver fileSaver,
  IFilePicker filePicker,
  IDeviceCache deviceCache) : ISettingsExporter
{
  private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

  public async Task<Result> ExportSettings()
  {
    var export = new SettingsExport
    {
      Username = settings.Username,
      ServerUri = settings.ServerUri,
      HideOfflineDevices = settings.HideOfflineDevices,
      NotifyUserSessionStart = settings.NotifyUserSessionStart,
      Devices = deviceCache.Devices.ToArray()
    };

    var fileName = $"ControlR_Settings_{DateTime.Now:yyyyMMdd-HHmmss}.json";

    using var ms = new MemoryStream();
    await JsonSerializer.SerializeAsync(ms, export, _jsonOptions);
    ms.Seek(0, SeekOrigin.Begin);

    var saveResult = await fileSaver.SaveAsync(fileName, ms);
    if (!saveResult.IsSuccessful)
    {
      if (saveResult.Exception is { } ex)
      {
        return Result.Fail(ex);
      }

      return Result.Fail("Unknown save failure.");
    }

    return Result.Ok();
  }

  public async Task<Result> ImportSettings()
  {
    var fileType = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
    {
      [DevicePlatform.Android] = ["application/json"],
      [DevicePlatform.WinUI] = [".json"]
    });

    var pickOptions = new PickOptions
    {
      PickerTitle = "Import ControlR Settings",
      FileTypes = fileType
    };
    var pickResult = await filePicker.PickAsync(pickOptions);

    if (pickResult is null)
    {
      return Result.Fail("Import cancelled");
    }

    await using var fs = await pickResult.OpenReadAsync();
    var export = await JsonSerializer.DeserializeAsync<SettingsExport>(fs);

    if (export is null)
    {
      return Result.Fail("Failed to deserialize settings export");
    }

    settings.Username = export.Username;
    settings.HideOfflineDevices = export.HideOfflineDevices;
    settings.NotifyUserSessionStart = export.NotifyUserSessionStart;

    foreach (var device in export.Devices)
    {
      if (!deviceCache.TryGet(device.Uid, out _))
      {
        await deviceCache.AddOrUpdate(device);
      }
    }

    settings.ServerUri = export.ServerUri;

    return Result.Ok();
  }
}