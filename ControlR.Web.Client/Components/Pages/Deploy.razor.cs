using ControlR.Web.Client.Extensions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace ControlR.Web.Client.Components.Pages;

public partial class Deploy
{
  private bool _addTags;
  private string? _installerKey;
  private InstallerKeyType _installerKeyType;
  private IEnumerable<TagResponseDto>? _selectedTags;
  private IReadOnlyList<TagResponseDto> _tags = [];
  private Guid? _tenantId;
  private DateTime? _inputExpirationDate;
  private TimeSpan? _inputExpirationTime;
  private string? _keyExpiration;
  private uint _totalUsesAllowed = 1;

  [Inject]
  public required AuthenticationStateProvider AuthState { get; init; }

  [Inject]
  public required TimeProvider TimeProvider { get; init; }

  [Inject]
  public required IClipboardManager Clipboard { get; init; }

  [Inject]
  public required IControlrApi ControlrApi { get; init; }

  [Inject]
  public required NavigationManager NavMan { get; init; }
  [Inject]
  public required ISnackbar Snackbar { get; init; }

  //private string MacArm64DeployScript
  //{
  //  get
  //  {
  //    var downloadUri = new Uri(GetServerUri(), "/downloads/osx-arm64/ControlR.Agent");
  //    return
  //      $"sudo rm -f /tmp/ControlR.Agent && " +
  //      $"sudo curl -o /tmp/ControlR.Agent {downloadUri} && " +
  //      $"sudo chmod +x /tmp/ControlR.Agent && " +
  //      $"sudo /tmp/ControlR.Agent install {GetCommonArgs()}";
  //  }
  //}

  //private string MacX64DeployScript
  //{
  //  get
  //  {
  //    var downloadUri = new Uri(GetServerUri(), "/downloads/osx-x64/ControlR.Agent");
  //    return
  //      $"sudo rm -f /tmp/ControlR.Agent && " +
  //      $"sudo curl -o /tmp/ControlR.Agent {downloadUri} && " +
  //      $"sudo chmod +x /tmp/ControlR.Agent && " +
  //      $"sudo /tmp/ControlR.Agent install {GetCommonArgs()}";
  //  }
  //}

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
        $"sudo wget -q -O /tmp/ControlR.Agent {downloadUri} && " +
        $"sudo chmod +x /tmp/ControlR.Agent && " +
        $"sudo /tmp/ControlR.Agent install {GetCommonArgs()}";
    }
  }

  private string WindowsDeployScript
  {
    get
    {
      var downloadUri = new Uri(GetServerUri(), "/downloads/win-x86/ControlR.Agent.exe");
      return "$ProgressPreference = \"SilentlyContinue\"; " +
             $"Invoke-WebRequest -Uri \"{downloadUri}\" -OutFile \"$env:TEMP/ControlR.Agent.exe\" -UseBasicParsing; " +
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

  //private async Task CopyMacX64Script()
  //{
  //  await Clipboard.SetText(MacX64DeployScript);
  //  Snackbar.Add("Install script copied to clipboard", Severity.Success);
  //}
  private async Task CopyWindowsScript()
  {
    if (_tenantId is null)
    {
      Snackbar.Add("Failed to find TenantId", Severity.Error);
      return;
    }

    await Clipboard.SetText(WindowsDeployScript);
    Snackbar.Add("Install script copied to clipboard", Severity.Success);
  }

  private async Task GenerateKey()
  {
    switch (_installerKeyType)
    {
      case InstallerKeyType.Unknown:
        Snackbar.Add("Token type is required", Severity.Error);
        return;
      case InstallerKeyType.UsageBased:
        await GenerateUsageBasedKey();
        break;
      case InstallerKeyType.TimeBased:
        await GenerateTimeBasedKey();
        break;
      default:
        break;
    }
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

    var dto = new CreateInstallerKeyRequestDto(InstallerKeyType.TimeBased, expirationDate);
    var createResult = await ControlrApi.CreateInstallerKey(dto);
    if (!createResult.IsSuccess)
    {
      Snackbar.Add("Failed to create installer key", Severity.Error);
      return;
    }
    _keyExpiration = expirationDate.ToString("g");
    _installerKey = createResult.Value.AccessKey;
  }

  private async Task GenerateUsageBasedKey()
  {
    if (_totalUsesAllowed < 1)
    {
      Snackbar.Add("Total uses must be greater than 0");
      return;
    }

    var dto = new CreateInstallerKeyRequestDto(InstallerKeyType.UsageBased, AllowedUses: _totalUsesAllowed);
    var createResult = await ControlrApi.CreateInstallerKey(dto);
    if (!createResult.IsSuccess)
    {
      Snackbar.Add("Failed to create installer key", Severity.Error);
      return;
    }
    _installerKey = createResult.Value.AccessKey;
    if (createResult.Value.Expiration.HasValue)
    {
      _keyExpiration = createResult.Value.Expiration.Value.ToLocalTime().ToString("g");
    }
  }

  //private async Task CopyMacArm64Script()
  //{
  //  await Clipboard.SetText(MacArm64DeployScript);
  //  Snackbar.Add("Install script copied to clipboard", Severity.Success);
  //}

  private string GetCommonArgs()
  {
    var serverUri = GetServerUri();
    var args = $"-s {serverUri} -i {serverUri.Authority} -t {_tenantId} -k {_installerKey}";

    if (!_addTags || _selectedTags is null) return args;

    var tags = string.Join(",", _selectedTags.Select(t => t.Id));
    args += $" -g {tags}";

    return args;
  }

  private Uri GetServerUri()
  {
    var currentUri = new Uri(NavMan.Uri);
    return new Uri($"{currentUri.Scheme}://{currentUri.Authority}");
  }
}
