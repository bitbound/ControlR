using ControlR.Libraries.Branding;
using Microsoft.AspNetCore.Components.Authorization;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.Customers;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.DeploymentOptions;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.InstallerKeys;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.Tags;

namespace ControlR.Web.Client.Components.Pages;

public partial class Deploy
{
  private readonly SemaphoreSlim _tagCapabilityLock = new(1, 1);

  private bool _addTags;
  private bool _appendInstanceId = true;
  private bool _canAssignDeviceTags;
  private bool _canReadCustomers;
  private IReadOnlyList<CustomerDto> _customers = [];
  private string? _deviceId;
  private IEnumerable<InstallerKeyDto> _existingKeys = [];
  private string? _existingKeySecretInput;
  private string? _friendlyName;
  private DateTime? _inputExpirationDate;
  private TimeSpan? _inputExpirationTime;
  private Guid? _installerKeyId;
  private string? _installerKeySecret;
  private InstallerKeyType _installerKeyType;
  private string? _instanceId;
  private string? _keyExpiration;
  private CustomerDto? _selectedCustomer;
  private InstallerKeyDto? _selectedExistingKey;
  private IReadOnlyCollection<TagResponseDto>? _selectedTags;
  private TagResponseDto[] _tags = [];
  private Guid? _tenantId;
  private uint _totalUsesAllowed = 1;
  private bool _useExistingKey;

  [Inject]
  public required AuthenticationStateProvider AuthState { get; init; }

  [Inject]
  public required IClipboardManager Clipboard { get; init; }

  [Inject]
  public required IControlrApi ControlrApi { get; init; }

  [Inject]
  public required NavigationManager NavMan { get; init; }

  [Inject]
  public required ISnackbar Snackbar { get; init; }

  [Inject]
  public required TimeProvider TimeProvider { get; init; }

  private static Func<string?, string?> DeviceIdValidator => deviceId =>
  {
    if (string.IsNullOrEmpty(deviceId))
    {
      return null;
    }

    return Guid.TryParse(deviceId, out _)
      ? null
      : "Must be a valid GUID.";
  };

  private string LinuxDeployScript
  {
    get
    {
      var downloadUri = new Uri(GetServerUri(), AppConstants.GetInstallerDownloadPath(RuntimeId.LinuxX64));
      return
        $"sudo rm -f /tmp/{BrandingConstants.InstallerBaseName} && " +
        $"sudo curl -o /tmp/{BrandingConstants.InstallerBaseName} {downloadUri} && " +
        $"sudo chmod +x /tmp/{BrandingConstants.InstallerBaseName} && " +
        $"sudo /tmp/{BrandingConstants.InstallerBaseName} install {GetCommonArgs()}";
    }
  }
  private string MacArm64DeployScript
  {
    get
    {
      var downloadUri = new Uri(GetServerUri(), AppConstants.GetInstallerDownloadPath(RuntimeId.MacOsArm64));
      return
        $"sudo rm -f /tmp/{BrandingConstants.InstallerBaseName} && " +
        $"sudo curl -o /tmp/{BrandingConstants.InstallerBaseName} {downloadUri} && " +
        $"sudo chmod +x /tmp/{BrandingConstants.InstallerBaseName} && " +
        $"sudo /tmp/{BrandingConstants.InstallerBaseName} install {GetCommonArgs()}";
    }
  }
  private string MacX64DeployScript
  {
    get
    {
      var downloadUri = new Uri(GetServerUri(), AppConstants.GetInstallerDownloadPath(RuntimeId.MacOsX64));
      return
        $"sudo rm -f /tmp/{BrandingConstants.InstallerBaseName} && " +
        $"sudo curl -o /tmp/{BrandingConstants.InstallerBaseName} {downloadUri} && " +
        $"sudo chmod +x /tmp/{BrandingConstants.InstallerBaseName} && " +
        $"sudo /tmp/{BrandingConstants.InstallerBaseName} install {GetCommonArgs()}";
    }
  }
  private string SelectedTagsText =>
    _selectedTags is null
      ? ""
      : string.Join(", ", _selectedTags.Select(x => x.Name));
  private string WindowsX64DeployScript
  {
    get
    {
      var downloadUri = new Uri(GetServerUri(), AppConstants.GetInstallerDownloadPath(RuntimeId.WinX64));
      return "$ProgressPreference = 'SilentlyContinue'; " +
             $"Invoke-WebRequest -Uri \"{downloadUri}\" -OutFile \"$env:TEMP/{BrandingConstants.InstallerBaseName}.exe\" -UseBasicParsing; " +
             $"Start-Process -FilePath \"$env:TEMP/{BrandingConstants.InstallerBaseName}.exe\" -ArgumentList \"install {GetCommonArgs()}\" -Verb RunAs;";
    }
  }
  private string WindowsX86DeployScript
  {
    get
    {
      var downloadUri = new Uri(GetServerUri(), AppConstants.GetInstallerDownloadPath(RuntimeId.WinX86));
      return "$ProgressPreference = 'SilentlyContinue'; " +
             $"Invoke-WebRequest -Uri \"{downloadUri}\" -OutFile \"$env:TEMP/{BrandingConstants.InstallerBaseName}.exe\" -UseBasicParsing; " +
             $"Start-Process -FilePath \"$env:TEMP/{BrandingConstants.InstallerBaseName}.exe\" -ArgumentList \"install {GetCommonArgs()}\" -Verb RunAs;";
    }
  }

  protected override async Task OnInitializedAsync()
  {
    await base.OnInitializedAsync();

    var state = await AuthState.GetAuthenticationStateAsync();
    if (state.User.TryGetTenantId(out var tenantId))
    {
      _tenantId = tenantId;
    }

    _canAssignDeviceTags = await GetTagCapability();
    _canReadCustomers = state.User.HasClientPolicy(PolicyNames.RequireCustomersRead);

    if (_tenantId is not { } deploymentTenantId)
    {
      Snackbar.Add(ClaimsPrincipalExtensions.NoTenantMessage, Severity.Error);
      return;
    }

    var deploymentOptionsResult = await ControlrApi.V1.DeploymentOptions.GetDeploymentOptions(deploymentTenantId);
    if (!deploymentOptionsResult.IsSuccess)
    {
      Snackbar.Add(deploymentOptionsResult.Reason, Severity.Error);
      return;
    }

    _appendInstanceId = deploymentOptionsResult.Value.AppendInstanceId;
    _instanceId = deploymentOptionsResult.Value.InstanceId;

    if (_canReadCustomers)
    {
      var customersResult = await ControlrApi.V1.Customers.GetAllCustomers(deploymentTenantId);
      if (customersResult.IsSuccess)
      {
        _customers = customersResult.Value.Items;
      }
      else
      {
        Snackbar.Add(customersResult.Reason, Severity.Error);
      }
    }

    if (!_canAssignDeviceTags)
    {
      return;
    }

    if (_tenantId is not { } tagsTenantId)
    {
      return;
    }

    var result = await ControlrApi.V1.Tags.GetAllTags(tagsTenantId);
    if (result.IsSuccess)
    {
      _tags = [.. result.Value.Items];
    }
    else
    {
      Snackbar.Add("Failed to get tags", Severity.Error);
    }
  }

  private async Task CopyInstallerKey()
  {
    if (_installerKeySecret is null)
    {
      return;
    }

    await Clipboard.SetText(_installerKeySecret);
    Snackbar.Add("Installer key copied to clipboard", Severity.Success);
  }

  private async Task CopyLinuxScript()
  {
    if (_tenantId is null)
    {
      Snackbar.Add(ClaimsPrincipalExtensions.NoTenantMessage, Severity.Error);
      return;
    }

    await Clipboard.SetText(LinuxDeployScript);
    Snackbar.Add("Install script copied to clipboard", Severity.Success);
  }

  private async Task CopyMacArm64Script()
  {
    await Clipboard.SetText(MacArm64DeployScript);
    Snackbar.Add("Install script copied to clipboard", Severity.Success);
  }

  private async Task CopyMacX64Script()
  {
    await Clipboard.SetText(MacX64DeployScript);
    Snackbar.Add("Install script copied to clipboard", Severity.Success);
  }

  private async Task CopyWindowsX64Script()
  {
    if (_tenantId is null)
    {
      Snackbar.Add(ClaimsPrincipalExtensions.NoTenantMessage, Severity.Error);
      return;
    }

    await Clipboard.SetText(WindowsX64DeployScript);
    Snackbar.Add("Install script copied to clipboard", Severity.Success);
  }

  private async Task CopyWindowsX86Script()
  {
    if (_tenantId is null)
    {
      Snackbar.Add(ClaimsPrincipalExtensions.NoTenantMessage, Severity.Error);
      return;
    }

    await Clipboard.SetText(WindowsX86DeployScript);
    Snackbar.Add("Install script copied to clipboard", Severity.Success);
  }

  private async Task GenerateKey()
  {
    switch (_installerKeyType)
    {
      case InstallerKeyType.Unknown:
        Snackbar.Add("Token type is required", Severity.Error);
        return;
      case InstallerKeyType.Persistent:
        await GeneratePersistentKey();
        break;
      case InstallerKeyType.UsageBased:
        await GenerateUsageBasedKey();
        break;
      case InstallerKeyType.TimeBased:
        await GenerateTimeBasedKey();
        break;
    }
  }

  private async Task GeneratePersistentKey()
  {
    if (_tenantId is not { } tenantId)
    {
      Snackbar.Add(ClaimsPrincipalExtensions.NoTenantMessage, Severity.Error);
      return;
    }

    var dto = new CreateInstallerKeyRequestDto(
      TenantId: tenantId,
      KeyType: InstallerKeyType.Persistent,
      FriendlyName: _friendlyName);

    var createResult = await ControlrApi.V1.InstallerKeys.CreateInstallerKey(dto);
    if (!createResult.IsSuccess)
    {
      Snackbar.Add(createResult.Reason, Severity.Error);
      return;
    }

    _installerKeySecret = createResult.Value.KeySecret;
    _installerKeyId = createResult.Value.Id;
  }

  private async Task GenerateTimeBasedKey()
  {
    if (_tenantId is not { } tenantId)
    {
      Snackbar.Add(ClaimsPrincipalExtensions.NoTenantMessage, Severity.Error);
      return;
    }

    if (_inputExpirationDate is null || _inputExpirationTime is null)
    {
      Snackbar.Add("Expiration date and time are required", Severity.Error);
      return;
    }

    var expirationDate = _inputExpirationDate.Value
      .Add(_inputExpirationTime.Value)
      .ToDateTimeOffset();

    if (expirationDate < TimeProvider.GetLocalNow())
    {
      Snackbar.Add("Expiration date must be in the future", Severity.Error);
      return;
    }

    var dto = new CreateInstallerKeyRequestDto(
      TenantId: tenantId,
      KeyType: InstallerKeyType.TimeBased,
      Expiration: expirationDate,
      FriendlyName: _friendlyName);

    var createResult = await ControlrApi.V1.InstallerKeys.CreateInstallerKey(dto);
    if (!createResult.IsSuccess)
    {
      Snackbar.Add(createResult.Reason, Severity.Error);
      return;
    }

    _keyExpiration = expirationDate.ToString("g");
    _installerKeySecret = createResult.Value.KeySecret;
    _installerKeyId = createResult.Value.Id;
  }

  private async Task GenerateUsageBasedKey()
  {
    if (_tenantId is not { } tenantId)
    {
      Snackbar.Add(ClaimsPrincipalExtensions.NoTenantMessage, Severity.Error);
      return;
    }

    if (_totalUsesAllowed < 1)
    {
      Snackbar.Add("Total uses must be greater than 0");
      return;
    }

    var dto = new CreateInstallerKeyRequestDto(
      TenantId: tenantId,
      KeyType: InstallerKeyType.UsageBased,
      AllowedUses: _totalUsesAllowed,
      FriendlyName: _friendlyName);

    var createResult = await ControlrApi.V1.InstallerKeys.CreateInstallerKey(dto);
    if (!createResult.IsSuccess)
    {
      Snackbar.Add(createResult.Reason, Severity.Error);
      return;
    }

    _installerKeySecret = createResult.Value.KeySecret;
    _installerKeyId = createResult.Value.Id;
    if (createResult.Value.Expiration.HasValue)
    {
      _keyExpiration = createResult.Value.Expiration.Value.ToLocalTime().ToString("g");
    }
  }

  private string GetCommonArgs()
  {
    var serverUri = GetServerUri();
    var args = $"-s {serverUri} -t {_tenantId} -ks {_installerKeySecret}";

    if (GetEffectiveInstanceId() is { } instanceId)
    {
      args += $" -i {instanceId}";
    }

    if (_installerKeyId.HasValue)
    {
      args += $" -ki {_installerKeyId}";
    }

    if (!string.IsNullOrWhiteSpace(_deviceId) && Guid.TryParse(_deviceId, out _))
    {
      args += $" -d {_deviceId}";
    }

    if (_selectedCustomer is not null)
    {
      args += $" -c {_selectedCustomer.Id}";
    }

    if (!_addTags || _selectedTags?.Any() != true)
    {
      return args;
    }

    var tags = string.Join(",", _selectedTags.Select(t => t.Id));
    args += $" -g {tags}";

    return args;
  }

  private string? GetEffectiveInstanceId()
  {
    if (!_appendInstanceId)
    {
      return null;
    }

    if (!string.IsNullOrWhiteSpace(_instanceId))
    {
      return _instanceId;
    }

    return GetServerUri().Host;
  }

  private string GetInstallerKeyDisplay(InstallerKeyDto? key)
  {
    if (key is null)
    {
      return string.Empty;
    }

    var name = string.IsNullOrWhiteSpace(key.FriendlyName) ? "Unnamed Key" : key.FriendlyName;
    return $"{name} ({key.KeyType}) - {key.Id}";
  }

  private Uri GetServerUri()
  {
    var currentUri = new Uri(NavMan.Uri);
    return new Uri($"{currentUri.Scheme}://{currentUri.Authority}");
  }

  private async Task<bool> GetTagCapability()
  {
    if (_tenantId is not { } tenantId)
    {
      return false;
    }

    Guid? deviceId = Guid.TryParse(_deviceId, out var parsedDeviceId)
      ? parsedDeviceId
      : null;

    var request = new DeploymentTagCapabilityRequestDto(
      deviceId,
      _selectedCustomer?.Id);

    var result = await ControlrApi.V1.DeploymentOptions.GetTagCapability(tenantId, request);
    if (!result.IsSuccess)
    {
      return false;
    }

    return result.Value.Allowed;
  }

  private async Task OnCustomerChanged(CustomerDto? customer)
  {
    _selectedCustomer = customer;
    await RefreshTagCapability();
  }

  private async Task OnDeviceIdChanged(string? deviceId)
  {
    _deviceId = deviceId;
    await RefreshTagCapability();
  }

  private void OnInstallerKeyTypeChanged(InstallerKeyType keyType)
  {
    _installerKeyType = keyType;
    if (keyType == InstallerKeyType.TimeBased)
    {
      var expiration = TimeProvider.GetLocalNow().AddHours(1);
      _inputExpirationTime = expiration.TimeOfDay;
      _inputExpirationDate = expiration.Date;
    }
  }

  private async Task RefreshTagCapability()
  {
    await _tagCapabilityLock.WaitAsync();
    try
    {
      var allowed = await GetTagCapability();
      if (_canAssignDeviceTags == allowed)
      {
        return;
      }

      _canAssignDeviceTags = allowed;
      if (!allowed)
      {
        // The target can no longer be tagged. Clear the tag selection and checkbox so stale
        // selections cannot be submitted.
        _addTags = false;
        _selectedTags = null;
      }
      else if (_tags.Length == 0)
      {
        // The target became taggable (e.g. by narrowing to an allowed existing device). Load the
        // available tags now so the selector has options.
        if (_tenantId is not { } tagsTenantId)
        {
          return;
        }

        var result = await ControlrApi.V1.Tags.GetAllTags(tagsTenantId);
        if (result.IsSuccess)
        {
          _tags = [.. result.Value.Items];
        }
        else
        {
          Snackbar.Add("Failed to get tags", Severity.Error);
        }
      }

      await InvokeAsync(StateHasChanged);
    }
    catch (Exception ex)
    {
      Snackbar.Add($"Failed to refresh tag capability: {ex.Message}", Severity.Error);
    }
    finally
    {
      _tagCapabilityLock.Release();
    }
  }

  private void ResetToKeySelection()
  {
    _installerKeySecret = null;
    _installerKeyId = null;
    _keyExpiration = null;
    _useExistingKey = false;
    _selectedExistingKey = null;
    _existingKeySecretInput = null;
  }

  private async Task ToggleKeyMode(bool useExisting)
  {
    _useExistingKey = useExisting;
    if (_useExistingKey && !_existingKeys.Any() && _tenantId is { } existingKeyTenantId)
    {
      var result = await ControlrApi.V1.InstallerKeys.GetAllInstallerKeys(existingKeyTenantId);
      if (result.IsSuccess)
      {
        _existingKeys = [.. result.Value.Items.OrderByDescending(x => x.CreatedAt)];
      }
      else
      {
        Snackbar.Add("Failed to load existing keys", Severity.Error);
      }
    }
  }

  private void UseExistingKey()
  {
    if (_selectedExistingKey is null)
    {
      Snackbar.Add("Please select a key", Severity.Warning);
      return;
    }
    if (string.IsNullOrWhiteSpace(_existingKeySecretInput))
    {
      Snackbar.Add("Please enter the key secret", Severity.Warning);
      return;
    }

    _installerKeyId = _selectedExistingKey.Id;
    _installerKeySecret = _existingKeySecretInput;
    _installerKeyType = _selectedExistingKey.KeyType;

    if (_selectedExistingKey.Expiration.HasValue)
    {
      _keyExpiration = _selectedExistingKey.Expiration.Value.ToLocalTime().ToString("g");
    }
    _totalUsesAllowed = _selectedExistingKey.AllowedUses ?? 0;
  }
}
