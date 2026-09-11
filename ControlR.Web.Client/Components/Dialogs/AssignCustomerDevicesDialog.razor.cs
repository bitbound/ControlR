using ControlR.Libraries.Api.Contracts.FilterSort;
using CustDtos = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.Customers;
using Microsoft.AspNetCore.Components.Authorization;

namespace ControlR.Web.Client.Components.Dialogs;

public partial class AssignCustomerDevicesDialog : ComponentBase
{
  private const int PageSize = 10;

  private readonly HashSet<Guid> _removedIds = [];
  private readonly HashSet<Guid> _selectedIds = [];

  private int _currentPage = 1;
  private List<DeviceResponseDto> _devices = [];
  private bool _loading;
  private string _searchText = string.Empty;
  private Guid _tenantId;
  private int _totalPages = 1;

  [Inject]
  public required AuthenticationStateProvider AuthState { get; init; }

  [Inject]
  public required IControlrApi ControlrApi { get; init; }

  [Parameter]
  public required Guid CustomerId { get; set; }

  [Inject]
  public required ILogger<AssignCustomerDevicesDialog> Logger { get; init; }

  [CascadingParameter]
  public required IMudDialogInstance MudDialog { get; init; }

  [Inject]
  public required ISnackbar Snackbar { get; init; }

  private bool HasChanges => _selectedIds.Count > 0 || _removedIds.Count > 0;
  private int TotalChanges => _selectedIds.Count + _removedIds.Count;

  protected override async Task OnInitializedAsync()
  {
    var state = await AuthState.GetAuthenticationStateAsync();
    if (state.User.TryGetTenantId(out var tenantId))
    {
      _tenantId = tenantId;
    }

    await LoadDevices();
  }

  private async Task Assign()
  {
    try
    {
      var result = await ControlrApi.V1.Customers.AssignCustomerDevices(
        CustomerId,
        _tenantId,
        new CustDtos.AssignCustomerDevicesRequestDto([.. _selectedIds], [.. _removedIds]));

      if (!result.IsSuccess)
      {
        Snackbar.Add(result.Reason, Severity.Error);
        return;
      }

      var changes = new List<string>();
      if (_selectedIds.Count > 0)
      {
        changes.Add($"{_selectedIds.Count} assigned");
      }
      if (_removedIds.Count > 0)
      {
        changes.Add($"{_removedIds.Count} unassigned");
      }

      Snackbar.Add(string.Join(" and ", changes), Severity.Success);
      MudDialog.Close(DialogResult.Ok(true));
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Failed to assign devices to customer {CustomerId}.", CustomerId);
      Snackbar.Add("Failed to assign devices.", Severity.Error);
    }
  }

  private void Cancel() => MudDialog.Cancel();

  private bool IsChecked(DeviceResponseDto device)
  {
    if (_removedIds.Contains(device.Id))
    {
      return false;
    }

    return (device.CustomerId.HasValue && device.CustomerId.Value == CustomerId) ||
      _selectedIds.Contains(device.Id);
  }

  private async Task LoadDevices()
  {
    _loading = true;
    await InvokeAsync(StateHasChanged);

    try
    {
      var request = new DeviceSearchRequestDto
      {
        SearchText = _searchText,
        HideOfflineDevices = false,
        Page = _currentPage - 1,
        PageSize = PageSize,
        SortDefinitions = [new DeviceColumnSort { PropertyName = nameof(DeviceResponseDto.Name), Descending = false, SortOrder = 0 }]
      };

      var response = await ControlrApi.Internal.Devices.SearchDevices(request);
      if (!response.IsSuccess)
      {
        Snackbar.Add("Failed to load devices", Severity.Error);
        return;
      }

      _devices = [.. response.Value.Items ?? []];
      _totalPages = Math.Max(1, (int)Math.Ceiling(response.Value.TotalItems / (double)PageSize));
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Failed to load devices for customer {CustomerId}.", CustomerId);
      Snackbar.Add("Failed to load devices.", Severity.Error);
    }
    finally
    {
      _loading = false;
      StateHasChanged();
    }
  }

  private async Task OnPageChanged(int page)
  {
    _currentPage = page;
    await LoadDevices();
  }

  private async Task OnSearchChanged(string _)
  {
    _currentPage = 1;
    await LoadDevices();
  }

  private void ToggleSelection(DeviceResponseDto device, bool isSelected)
  {
    if (!device.CustomerId.HasValue || device.CustomerId.Value != CustomerId)
    {
      if (isSelected)
      {
        _selectedIds.Add(device.Id);
      }
      else
      {
        _selectedIds.Remove(device.Id);
      }
    }
    else
    {
      if (isSelected)
      {
        _removedIds.Remove(device.Id);
      }
      else
      {
        _removedIds.Add(device.Id);
      }
    }
  }
}
