using CommunityToolkit.Maui.Storage;
using System.Text.Json;

namespace ControlR.Viewer.Services;

internal interface ISettingsExporter
{
    Task<Result> ExportSettings();
    Task<Result> ImportSettings();
}
internal class SettingsExporter(
    ISettings _settings,
    IFileSaver _fileSaver,
    IFilePicker _filePicker,
    IDeviceCache _deviceCache) : ISettingsExporter
{
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };
    public async Task<Result> ExportSettings()
    {
        var export = new SettingsExport()
        {
            Username = _settings.Username,
            ServerUri = _settings.ServerUri,
            HideOfflineDevices = _settings.HideOfflineDevices,
            NotifyUserSessionStart = _settings.NotifyUserSessionStart,
            Devices = _deviceCache.Devices.ToArray()
        };

        var fileName = $"ControlR_Settings_{DateTime.Now:yyyyMMdd-HHmmss}.json";

        using var ms = new MemoryStream();
        await JsonSerializer.SerializeAsync(ms, export, _jsonOptions);
        ms.Seek(0, SeekOrigin.Begin);

        var saveResult = await _fileSaver.SaveAsync(fileName, ms);
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
        var fileType = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>()
        {
            [DevicePlatform.Android] = ["application/json"],
            [DevicePlatform.WinUI] = [".json"]
        });

        var pickOptions = new PickOptions()
        {
            PickerTitle = "Import ControlR Settings",
            FileTypes = fileType
        };
        var pickResult = await _filePicker.PickAsync(pickOptions);

        if (pickResult is null)
        {
            return Result.Fail("Import cancelled");
        }

        using var fs = await pickResult.OpenReadAsync();
        var export = await JsonSerializer.DeserializeAsync<SettingsExport>(fs);

        if (export is null)
        {
            return Result.Fail("Failed to deserialize settings export");
        }

        _settings.Username = export.Username;
        _settings.HideOfflineDevices = export.HideOfflineDevices;
        _settings.NotifyUserSessionStart = export.NotifyUserSessionStart;

        foreach (var device in export.Devices)
        {
            if (!_deviceCache.TryGet(device.Id, out _))
            {
                await _deviceCache.AddOrUpdate(device);
            }
        }

        _settings.ServerUri = export.ServerUri;

        return Result.Ok();
    }
}