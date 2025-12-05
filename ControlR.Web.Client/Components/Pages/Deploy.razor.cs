using ControlR.Web.Client.Extensions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace ControlR.Web.Client.Components.Pages;

public partial class Deploy
{
  private bool _addTags;
  private string? _deviceId;
  private string? _existingKeySecretInput;
  private IEnumerable<AgentInstallerKeyDto> _existingKeys = [];
  private string? _friendlyName;
  private DateTime? _inputExpirationDate;
  private TimeSpan? _inputExpirationTime;
  private Guid? _installerKeyId;
  private string? _installerKeySecret;
  private InstallerKeyType _installerKeyType;
  private string? _keyExpiration;
  private AgentInstallerKeyDto? _selectedExistingKey;
  private IEnumerable<TagResponseDto>? _selectedTags;
  private IReadOnlyList<TagResponseDto> _tags = [];
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

  private string MacArm64DeployScript
  {
    get
    {
      var downloadUri = new Uri(GetServerUri(), "/downloads/osx-arm64/ControlR.Agent");
      return
        $"sudo rm -f /tmp/ControlR.Agent && " +
        $"sudo curl -o /tmp/ControlR.Agent {downloadUri} && " +
        $"sudo chmod +x /tmp/ControlR.Agent && " +
        $"sudo /tmp/ControlR.Agent install {GetCommonArgs()}";
    }
  }
  private string MacX64DeployScript
  {
    get
    {
      var downloadUri = new Uri(GetServerUri(), "/downloads/osx-x64/ControlR.Agent");
      return
        $"sudo rm -f /tmp/ControlR.Agent && " +
        $"sudo curl -o /tmp/ControlR.Agent {downloadUri} && " +
        $"sudo chmod +x /tmp/ControlR.Agent && " +
        $"sudo /tmp/ControlR.Agent install {GetCommonArgs()}";
    }
  }
  private string SelectedTagsText =>
    _selectedTags is null
      ? ""
      : string.Join(", ", _selectedTags.Select(x => x.Name));
  private string UbuntuDeployScript
  {
    get
    {
      var downloadUri = new Uri(GetServerUri(), "/downloads/linux-x64/ControlR.Agent");
      return
        $"sudo rm -f /tmp/ControlR.Agent && " +
        $"sudo wget -O /tmp/ControlR.Agent {downloadUri} && " +
        $"sudo chmod +x /tmp/ControlR.Agent && " +
        $"sudo /tmp/ControlR.Agent install {GetCommonArgs()}";
    }
  }
  private string WindowsX64DeployScript
  {
    get
    {
      var downloadUri = new Uri(GetServerUri(), "/downloads/win-x64/ControlR.Agent.exe");
      return $"Invoke-WebRequest -Uri \"{downloadUri}\" -OutFile \"$env:TEMP/ControlR.Agent.exe\" -UseBasicParsing; " +
             $"Start-Process -FilePath \"$env:TEMP/ControlR.Agent.exe\" -ArgumentList \"install {GetCommonArgs()}\" -Verb RunAs;";
    }
  }
  private string WindowsX86DeployScript
  {
    get
    {
      var downloadUri = new Uri(GetServerUri(), "/downloads/win-x86/ControlR.Agent.exe");
      return $"Invoke-WebRequest -Uri \"{downloadUri}\" -OutFile \"$env:TEMP/ControlR.Agent.exe\" -UseBasicParsing; " +
             $"Start-Process -FilePath \"$env:TEMP/ControlR.Agent.exe\" -ArgumentList \"install {GetCommonArgs()}\" -Verb RunAs;";
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

    var result = await ControlrApi.GetAllowedTags();
    if (result.IsSuccess)
    {
      _tags = result.Value;
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

  private async Task CopyUbuntuScript()
  {
    if (_tenantId is null)
    {
      Snackbar.Add("Failed to find TenantId", Severity.Error);
      return;
    }

    await Clipboard.SetText(UbuntuDeployScript);
    Snackbar.Add("Install script copied to clipboard", Severity.Success);
  }

  private async Task CopyWindowsX64Script()
  {
    if (_tenantId is null)
    {
      Snackbar.Add("Failed to find TenantId", Severity.Error);
      return;
    }

    await Clipboard.SetText(WindowsX64DeployScript);
    Snackbar.Add("Install script copied to clipboard", Severity.Success);
  }

  private async Task CopyWindowsX86Script()
  {
    if (_tenantId is null)
    {
      Snackbar.Add("Failed to find TenantId", Severity.Error);
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
    var dto = new CreateInstallerKeyRequestDto(
      KeyType: InstallerKeyType.Persistent,
      FriendlyName: _friendlyName);

    var createResult = await ControlrApi.CreateInstallerKey(dto);
    if (!createResult.IsSuccess)
    {
      Snackbar.Add("Failed to create installer key", Severity.Error);
      return;
    }

    _installerKeySecret = createResult.Value.KeySecret;
    _installerKeyId = createResult.Value.Id;
  }

  private async Task GenerateTimeBasedKey()
  {
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
      KeyType: InstallerKeyType.TimeBased,
      Expiration: expirationDate,
      FriendlyName: _friendlyName);

    var createResult = await ControlrApi.CreateInstallerKey(dto);
    if (!createResult.IsSuccess)
    {
      Snackbar.Add("Failed to create installer key", Severity.Error);
      return;
    }

    _keyExpiration = expirationDate.ToString("g");
    _installerKeySecret = createResult.Value.KeySecret;
    _installerKeyId = createResult.Value.Id;
  }

  private async Task GenerateUsageBasedKey()
  {
    if (_totalUsesAllowed < 1)
    {
      Snackbar.Add("Total uses must be greater than 0");
      return;
    }

    var dto = new CreateInstallerKeyRequestDto(
      KeyType: InstallerKeyType.UsageBased,
      AllowedUses: _totalUsesAllowed,
      FriendlyName: _friendlyName);

    var createResult = await ControlrApi.CreateInstallerKey(dto);
    if (!createResult.IsSuccess)
    {
      Snackbar.Add("Failed to create installer key", Severity.Error);
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
    var args = $"-s {serverUri} -i {serverUri.Authority} -t {_tenantId} -ks {_installerKeySecret}";

    if (_installerKeyId.HasValue)
    {
      args += $" -ki {_installerKeyId}";
    }

    if (!string.IsNullOrWhiteSpace(_deviceId) && Guid.TryParse(_deviceId, out _))
    {
      args += $" -d {_deviceId}";
    }

    if (!_addTags || _selectedTags?.Any() != true)
    {
      return args;
    }

    var tags = string.Join(",", _selectedTags.Select(t => t.Id));
    args += $" -g {tags}";

    return args;
  }

  private string GetInstallerKeyDisplay(AgentInstallerKeyDto? key)
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

  private async Task ToggleKeyMode(bool useExisting)
  {
    _useExistingKey = useExisting;
    if (_useExistingKey && !_existingKeys.Any())
    {
      var result = await ControlrApi.GetAllInstallerKeys();
      if (result.IsSuccess)
      {
        _existingKeys = result.Value.OrderByDescending(x => x.CreatedAt);
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
