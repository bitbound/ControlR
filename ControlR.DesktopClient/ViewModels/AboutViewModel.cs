using System.Diagnostics;
using ControlR.DesktopClient.Common.Options;
using Microsoft.Extensions.Options;

namespace ControlR.DesktopClient.ViewModels;

public interface IAboutViewModel : IViewModelBase
{
  string? AppVersion { get; }
  string InstanceId { get; }
  IRelayCommand<string> OpenUrlCommand { get; }
}

public partial class AboutViewModel(
  IOptionsMonitor<DesktopClientOptions> options) : ViewModelBase<AboutView>, IAboutViewModel
{
  private readonly IOptionsMonitor<DesktopClientOptions> _options = options;

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

    try
    {
      Process.Start(new ProcessStartInfo
      {
        FileName = url,
        UseShellExecute = true
      });
    }
    catch
    {
      // Silently fail if browser cannot be opened
    }
  }
}
