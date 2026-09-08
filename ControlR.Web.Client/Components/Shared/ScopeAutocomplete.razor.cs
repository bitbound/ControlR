namespace ControlR.Web.Client.Components.Shared;

public sealed record ScopeOption(Guid Id, string DisplayName);

public partial class ScopeAutocomplete
{
  private bool _initialized;
  private IReadOnlyList<ScopeOption> _options = [];
  private PermissionScopeKind _previousScopeKind;
  private ScopeOption? _selected;
  private DeviceResponseDto? _selectedDevice;

  [Parameter]
  public string? Class { get; set; }

  [Inject]
  public required IControlrApi ControlrApi { get; init; }

  [Inject]
  public required IDialogService DialogService { get; init; }

  [Parameter]
  public string Label { get; set; } = "Scope";

  [Parameter]
  public PermissionScopeKind ScopeKind { get; set; } = PermissionScopeKind.Tenant;

  [Parameter]
  public Guid? SelectedId { get; set; }

  [Parameter]
  public EventCallback<Guid?> SelectedIdChanged { get; set; }

  private string DeviceDisplayText =>
    _selectedDevice is null ? string.Empty : DeviceDisplay.GetFullDisplayName(_selectedDevice);
  private string ImplicitScopeMessage => ScopeKind switch
  {
    PermissionScopeKind.Server => "Server-wide scope. No specific target is required.",
    _ => "Tenant-wide scope. No specific target is required."
  };
  private bool IsDeviceScope => ScopeKind == PermissionScopeKind.Device;
  private bool RequiresTarget => ScopeKind is PermissionScopeKind.Device or PermissionScopeKind.DeviceGroup or PermissionScopeKind.CustomerTenant or PermissionScopeKind.UserGroup;

  protected override async Task OnParametersSetAsync()
  {
    if (ScopeKind == _previousScopeKind)
    {
      return;
    }

    var isFirstLoad = !_initialized;
    _initialized = true;
    _previousScopeKind = ScopeKind;
    _selected = null;
    _selectedDevice = null;

    // Preserve a pre-selected target on initial load (edit mode); clear it on later kind changes.
    if (!isFirstLoad || SelectedId is null)
    {
      await SelectedIdChanged.InvokeAsync(null);
    }
    else if (IsDeviceScope)
    {
      var deviceResult = await ControlrApi.Internal.Devices.GetDevice(SelectedId.Value);
      if (deviceResult.IsSuccess && deviceResult.Value is not null)
      {
        _selectedDevice = deviceResult.Value;
      }
    }

    await LoadOptions();

    if (SelectedId is { } selectedId)
    {
      _selected = _options.FirstOrDefault(x => x.Id == selectedId);
    }
  }

  private static bool Matches(string? value, string query) =>
    string.IsNullOrWhiteSpace(query) || (value?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false);

  private async Task ClearDevice()
  {
    _selectedDevice = null;
    await SelectedIdChanged.InvokeAsync(null);
  }

  private async Task HandleValueChanged(ScopeOption? value)
  {
    _selected = value;
    await SelectedIdChanged.InvokeAsync(value?.Id);
  }

  private async Task<IReadOnlyList<ScopeOption>> LoadCustomers(CancellationToken cancellationToken)
  {
    var result = await ControlrApi.Internal.Customers.GetAll(cancellationToken);
    if (!result.IsSuccess)
    {
      return [];
    }

    return [.. result.Value
      .OrderBy(x => x.Name)
      .Select(x => new ScopeOption(x.Id, x.Name))];
  }

  private async Task<IReadOnlyList<ScopeOption>> LoadDeviceGroups(CancellationToken cancellationToken)
  {
    var result = await ControlrApi.Internal.DeviceGroups.GetAll(cancellationToken);
    if (!result.IsSuccess)
    {
      return [];
    }

    return [.. result.Value
      .OrderBy(x => x.Name)
      .Select(x => new ScopeOption(x.Id, x.Name))];
  }

  private async Task LoadOptions(CancellationToken cancellationToken = default)
  {
    _options = ScopeKind switch
    {
      PermissionScopeKind.CustomerTenant => await LoadCustomers(cancellationToken),
      PermissionScopeKind.DeviceGroup => await LoadDeviceGroups(cancellationToken),
      PermissionScopeKind.UserGroup => await LoadUserGroups(cancellationToken),
      _ => []
    };
  }

  private async Task<IReadOnlyList<ScopeOption>> LoadUserGroups(CancellationToken cancellationToken)
  {
    var result = await ControlrApi.Internal.UserGroups.GetAll(cancellationToken);
    if (!result.IsSuccess)
    {
      return [];
    }

    return [.. result.Value
      .OrderBy(x => x.Name)
      .Select(x => new ScopeOption(x.Id, x.Name))];
  }

  private async Task OpenDevicePicker()
  {
    var options = new DialogOptions { FullWidth = true, MaxWidth = MaxWidth.Medium };
    var dialog = await DialogService.ShowAsync<DevicePickerDialog>("Select Device", options);
    var result = await dialog.Result;

    if (result is null || result.Canceled || result.Data is not DeviceResponseDto device)
    {
      return;
    }

    _selectedDevice = device;
    await SelectedIdChanged.InvokeAsync(device.Id);
  }

  private Task<IEnumerable<ScopeOption>> Search(string query, CancellationToken cancellationToken)
  {
    IEnumerable<ScopeOption> matches = _options.Where(x => Matches(x.DisplayName, query));
    return Task.FromResult(matches);
  }
}
