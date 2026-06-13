using ControlR.Web.Client.Services;
using Microsoft.AspNetCore.Components.Web;
using ControlR.Libraries.Api.Contracts.Dtos.Devices;

namespace ControlR.Web.Client.Components.Shared;

public partial class DeviceOverviewGrid
{
  private string? _aliasValue;
  private bool _isEditingAlias;
  private bool _isSavingAlias;

  [Inject]
  public required IClipboardManager ClipboardManager { get; init; }
  [Inject]
  public required IControlrApi ControlrApi { get; init; }
  [Parameter]
  [EditorRequired]
  public required DeviceResponseDto Device { get; init; }
  [Inject]
  public required IDeviceStore DeviceStore { get; init; }
  [Inject]
  public required ILogger<DeviceOverviewGrid> Logger { get; init; }
  [CascadingParameter]
  public required Palette Palette { get; init; }
  [Inject]
  public required ISnackbar Snackbar { get; init; }
  [Inject]
  public required IHubConnection<IViewerHub> ViewerHub { get; init; }

  protected override Task OnInitializedAsync()
  {
    _aliasValue = Device.Alias;
    return base.OnInitializedAsync();
  }

  protected override void OnParametersSet()
  {
    _aliasValue = Device.Alias;
    base.OnParametersSet();
  }

  private static List<ChartSeries<double>> GetDriveChartSeries(Drive drive)
  {
    var usedSpace = drive.TotalSize - drive.FreeSpace;
    var freeSpace = drive.FreeSpace;
    return
    [
      new ChartSeries<double>
      {
        Data = new List<double> { usedSpace, freeSpace }
      }
    ];
  }

  private static string? ValidateAlias(string? value)
  {
    if (value is not null && value.Length > 100)
    {
      return "Alias must be 100 characters or fewer.";
    }

    return null;
  }

  private void CancelEditAlias()
  {
    _isEditingAlias = false;
    _aliasValue = Device.Alias;
  }

  private async Task CopyDeviceId()
  {
    try
    {
      await ClipboardManager.SetText(Device.Id.ToString());
      Snackbar.Add("Device ID copied to clipboard.", Severity.Success);
    }
    catch (Exception ex)
    {
      Snackbar.Add($"Failed to copy: {ex.Message}", Severity.Error);
    }
  }

  private string GetCurrentUsersDisplay()
  {
    if (Device.CurrentUsers == null || Device.CurrentUsers.Length == 0)
    {
      return "No active users";
    }

    if (Device.CurrentUsers.Length == 1)
    {
      return Device.CurrentUsers[0];
    }

    var currentUser = Device.CurrentUsers[0];
    var additionalUsersCount = Device.CurrentUsers.Length - 1;
    return $"{currentUser} and {additionalUsersCount} more";
  }

  private string GetUsedStorageDisplay()
  {
    var usedStorageGB = Device.UsedStorage.ToString("N0");
    var usedStoragePercent = (Device.UsedStoragePercent * 100).ToString("N2");
    return $"{usedStorageGB} GB ({usedStoragePercent}%)";
  }

  private async Task HandleAliasKeyDown(KeyboardEventArgs e)
  {
    if (e.Key == "Enter")
    {
      await SaveAlias();
    }
    else if (e.Key == "Escape")
    {
      CancelEditAlias();
    }
  }

  private async Task SaveAlias()
  {
    _isSavingAlias = true;

    try
    {
      var request = new UpdateDeviceAliasRequestDto(Device.Id, _aliasValue);
      var result = await ControlrApi.Devices.UpdateDeviceAlias(request);

      if (result.IsSuccess)
      {
        var updatedDevice = result.Value;
        await DeviceStore.AddOrUpdate(updatedDevice);
        _aliasValue = updatedDevice.Alias;
        await ViewerHub.Server.RefreshDeviceInfo(Device.Id);
        Snackbar.Add("Alias updated.", Severity.Success);
        _isEditingAlias = false;
      }
      else
      {
        Snackbar.Add("Failed to update alias.", Severity.Error);
      }
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Failed to update alias for device {DeviceId}.", Device.Id);
      Snackbar.Add("Failed to update alias.", Severity.Error);
    }
    finally
    {
      _isSavingAlias = false;
      await InvokeAsync(StateHasChanged);
    }
  }

  private void StartEditAlias()
  {
    _isEditingAlias = true;
  }
}
