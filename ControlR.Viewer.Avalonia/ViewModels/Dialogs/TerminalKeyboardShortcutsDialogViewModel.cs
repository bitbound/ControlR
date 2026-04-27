using ControlR.Libraries.Avalonia.Controls.Dialogs;
using ControlR.Libraries.Avalonia.ViewModels;
using ControlR.Viewer.Avalonia.Views.Dialogs;

namespace ControlR.Viewer.Avalonia.ViewModels.Dialogs;

public interface ITerminalKeyboardShortcutsDialogViewModel : IDisposable, IViewReference<TerminalKeyboardShortcutsDialogView>
{
  IRelayCommand CloseCommand { get; }
}

public partial class TerminalKeyboardShortcutsDialogViewModel(
  IDialogProvider dialogProvider) : ViewModelBase<TerminalKeyboardShortcutsDialogView>, ITerminalKeyboardShortcutsDialogViewModel
{
  private readonly IDialogProvider _dialogProvider = dialogProvider;

  [RelayCommand]
  private void Close()
  {
    _dialogProvider.Close();
  }
}