using ControlR.Assist.Services;
using ControlR.Libraries.Branding;
using CommunityToolkit.Mvvm.Input;

namespace ControlR.Assist.ViewModels;

public sealed partial class MainWindowViewModel : ViewModelBase
{
  private TaskCompletionSource<bool>? _confirmTaskCompletionSource;

  [ObservableProperty]
  private bool _isConfirmOpen;

  public MainWindowViewModel(HomeViewModel homeViewModel, INavigationService navigationService)
  {
    Home = homeViewModel;
    CurrentViewModel = homeViewModel;
    navigationService.NavigateRequested += viewModel => CurrentViewModel = viewModel;
    homeViewModel.RequestConfirmationAsync = ShowConfirmAsync;
  }

  [ObservableProperty]
  public partial string? ConfirmMessage { get; set; }

  [ObservableProperty]
  public partial ViewModelBase CurrentViewModel { get; set; }
  public HomeViewModel Home { get; }
  public string WindowTitle => $"{BrandingConstants.BrandName}  |  {Localization.AssistWindowTitle}";

  [RelayCommand]
  private void AcceptConfirm() => ResolveConfirm(approved: true);

  [RelayCommand]
  private void CancelConfirm() => ResolveConfirm(approved: false);

  private void ResolveConfirm(bool approved)
  {
    IsConfirmOpen = false;
    _confirmTaskCompletionSource?.TrySetResult(approved);
    _confirmTaskCompletionSource = null;
  }

  private Task<bool> ShowConfirmAsync(string message)
  {
    _confirmTaskCompletionSource?.TrySetResult(false);
    _confirmTaskCompletionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    ConfirmMessage = message;
    IsConfirmOpen = true;
    return _confirmTaskCompletionSource.Task;
  }
}
