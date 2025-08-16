using CommunityToolkit.Mvvm.ComponentModel;
using ControlR.DesktopClient.Common.Options;
using Microsoft.Extensions.Options;

namespace ControlR.DesktopClient.ViewModels;

public interface IMainWindowViewModel : IViewModelBase
{
  IViewModelBase CurrentViewModel { get; set; }
}

public partial class MainWindowViewModel(IOptions<DesktopClientOptions> options) : ViewModelBase, IMainWindowViewModel
{
  private readonly IOptions<DesktopClientOptions> _options = options;

  [ObservableProperty]
  private IViewModelBase? _currentViewModel;
}
