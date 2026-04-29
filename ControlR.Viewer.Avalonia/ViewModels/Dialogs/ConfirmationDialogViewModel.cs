using System.Windows.Input;
using ControlR.Libraries.Avalonia.Controls.Dialogs;
using ControlR.Libraries.Avalonia.ViewModels;
using ControlR.Viewer.Avalonia.Views.Dialogs;

namespace ControlR.Viewer.Avalonia.ViewModels.Dialogs;

public interface IConfirmationDialogViewModel : IDisposable, IViewReference<ConfirmationDialogView>
{
  IRelayCommand CancelCommand { get; }
  string CancelText { get; }
  Task<bool> Completion { get; }
  IRelayCommand ConfirmCommand { get; }
  string ConfirmText { get; }
  string Message { get; }
}
internal sealed partial class ConfirmationDialogViewModel : ViewModelBase<ConfirmationDialogView>, IConfirmationDialogViewModel
{
  private readonly IDialogProvider _dialogProvider;
  private readonly TaskCompletionSource<bool> _taskCompletionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);

  public ConfirmationDialogViewModel(
    IDialogProvider dialogProvider,
    string message,
    string confirmText,
    string cancelText)
  {
    _dialogProvider = dialogProvider;
    Message = message;
    ConfirmText = confirmText;
    CancelText = cancelText;
  }

  public string CancelText { get; }
  public Task<bool> Completion => _taskCompletionSource.Task;
  public string ConfirmText { get; }
  public string Message { get; }

  [RelayCommand]
  private void Cancel()
  {
    _taskCompletionSource.TrySetResult(false);
    _dialogProvider.Close();
  }

  [RelayCommand]
  private void Confirm()
  {
    _taskCompletionSource.TrySetResult(true);
    _dialogProvider.Close();
  }
}