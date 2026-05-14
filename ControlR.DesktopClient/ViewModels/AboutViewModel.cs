using ControlR.DesktopClient.Common.Options;
using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.DesktopClient.Common.ViewModels;
using Microsoft.Extensions.Options;

namespace ControlR.DesktopClient.ViewModels;

public interface IAboutViewModel : IViewModelBase
{
  string? AppVersion { get; }
  string InstanceId { get; }
  IRelayCommand<string> OpenUrlCommand { get; }
}

public partial class AboutViewModel(
  IOptionsMonitor<DesktopClientOptions> options,
  IUrlLauncher urlLauncher) : ViewModelBase<AboutView>, IAboutViewModel
{
  private readonly IOptionsMonitor<DesktopClientOptions> _options = options;
  private readonly IUrlLauncher _urlLauncher = urlLauncher;

  [ObservableProperty]
  private string? _appVersion;

  public string InstanceId => string.IsNullOrWhiteSpace(_options.CurrentValue.InstanceId) 
    ? $"({Localization.None.ToLower()})" 
    : _options.CurrentValue.InstanceId;

  protected override async Task OnInitializeAsync()
  {
    await base.OnInitializeAsync();
    AppVersion = typeof(AboutViewModel).Assembly.GetName().Version?.ToString();
  }

  [RelayCommand]
  private void OpenUrl(string? url)
  {
    if (string.IsNullOrWhiteSpace(url))
    {
      return;
    }

    _ = _urlLauncher.Open(url);
  }
}
