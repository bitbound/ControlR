using Microsoft.AspNetCore.Components.Authorization;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.Customers;

namespace ControlR.Web.Client.Components.Pages;

public partial class Customers : ComponentBase
{
  private bool _canWrite;
  private IEnumerable<CustomerDto> _customers = [];
  private bool _loading;
  private string _searchString = string.Empty;
  private Guid _tenantId;

  [Inject]
  public required AuthenticationStateProvider AuthState { get; init; }

  [Inject]
  public required IClipboardManager ClipboardManager { get; init; }

  [Inject]
  public required IControlrApi ControlrApi { get; init; }

  [Inject]
  public required IDialogService DialogService { get; init; }

  [Inject]
  public required ISnackbar Snackbar { get; init; }

  private Func<CustomerDto, bool> QuickFilter => customer =>
  {
    if (string.IsNullOrWhiteSpace(_searchString))
    {
      return true;
    }

    return customer.Name.Contains(_searchString, StringComparison.OrdinalIgnoreCase) ||
           (customer.Description?.Contains(_searchString, StringComparison.OrdinalIgnoreCase) ?? false) ||
           (customer.Notes?.Contains(_searchString, StringComparison.OrdinalIgnoreCase) ?? false);
  };

  protected override async Task OnInitializedAsync()
  {
    var state = await AuthState.GetAuthenticationStateAsync();
    _canWrite = state.User.HasClientPolicy(PolicyNames.RequireCustomersWrite);

    if (!state.User.TryGetTenantId(Snackbar, out var tenantId))
    {
      return;
    }

    _tenantId = tenantId;
    await Refresh();
  }

  private async Task AssignDevices(CustomerDto customer)
  {
    var parameters = new DialogParameters<AssignCustomerDevicesDialog>
    {
      { x => x.CustomerId, customer.Id }
    };

    var options = new DialogOptions
    {
      FullWidth = true,
      MaxWidth = MaxWidth.Medium
    };

    var dialog = await DialogService.ShowAsync<AssignCustomerDevicesDialog>($"Assign Devices to {customer.Name}", parameters, options);
    var result = await dialog.Result;

    if (result is not null && !result.Canceled)
    {
      await Refresh();
    }
  }

  private async Task CopyId(Guid id)
  {
    await ClipboardManager.SetText(id.ToString());
    Snackbar.Add("Copied to clipboard", Severity.Success);
  }

  private async Task CreateCustomer()
  {
    var options = new DialogOptions { FullWidth = true, MaxWidth = MaxWidth.Small };
    var dialog = await DialogService.ShowAsync<CustomerDialog>("Create Customer", options);
    var result = await dialog.Result;

    if (result is null || result.Canceled || result.Data is not CustomerDialogResult dialogResult)
    {
      return;
    }

    var createResult = await ControlrApi.V1.Customers.CreateCustomer(
      _tenantId,
      new CreateCustomerRequestDto(dialogResult.Name, dialogResult.Description, dialogResult.Notes));

    if (!createResult.IsSuccess)
    {
      Snackbar.Add(createResult.Reason, Severity.Error);
      return;
    }

    Snackbar.Add("Customer created", Severity.Success);
    await Refresh();
  }

  private async Task DeleteCustomer(CustomerDto customer)
  {
    var confirmed = await DialogService.ShowMessageBoxAsync(
      "Delete Customer",
      $"Are you sure you want to delete \"{customer.Name}\"? Devices assigned to this customer will become unassigned.",
      "Delete", "Cancel");

    if (!confirmed.GetValueOrDefault())
    {
      return;
    }

    var result = await ControlrApi.V1.Customers.DeleteCustomer(customer.Id, _tenantId);
    if (!result.IsSuccess)
    {
      Snackbar.Add(result.Reason, Severity.Error);
      return;
    }

    Snackbar.Add("Customer deleted", Severity.Success);
    await Refresh();
  }

  private async Task EditCustomer(CustomerDto customer)
  {
    var parameters = new DialogParameters<CustomerDialog>
    {
      { x => x.Name, customer.Name },
      { x => x.Description, customer.Description },
      { x => x.Notes, customer.Notes }
    };

    var options = new DialogOptions { FullWidth = true, MaxWidth = MaxWidth.Small };
    var dialog = await DialogService.ShowAsync<CustomerDialog>("Edit Customer", parameters, options);
    var result = await dialog.Result;

    if (result is null || result.Canceled || result.Data is not CustomerDialogResult dialogResult)
    {
      return;
    }

    var updateResult = await ControlrApi.V1.Customers.UpdateCustomer(
      customer.Id,
      _tenantId,
      new UpdateCustomerRequestDto(dialogResult.Name, dialogResult.Description, dialogResult.Notes));

    if (!updateResult.IsSuccess)
    {
      Snackbar.Add(updateResult.Reason, Severity.Error);
      return;
    }

    Snackbar.Add("Customer updated", Severity.Success);
    await Refresh();
  }

  private async Task Refresh()
  {
    _loading = true;
    StateHasChanged();

    try
    {
      var result = await ControlrApi.V1.Customers.GetAllCustomers(_tenantId);
      if (result.IsSuccess)
      {
        _customers = result.Value.Items;
      }
      else
      {
        Snackbar.Add(result.Reason, Severity.Error);
      }
    }
    finally
    {
      _loading = false;
      StateHasChanged();
    }
  }

  private string TruncateId(Guid id)
  {
    return $"{id.ToString()[..8]}...";
  }
}
