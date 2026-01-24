using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ControlR.DesktopClient.ViewModels;

public interface IAboutViewModel : IViewModelBase
{
  string? AppVersion { get; }
  IRelayCommand<string> OpenUrlCommand { get; }
}

public partial class AboutViewModel : ViewModelBase<AboutView>, IAboutViewModel
{
  [ObservableProperty]
  private string? _appVersion;

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
