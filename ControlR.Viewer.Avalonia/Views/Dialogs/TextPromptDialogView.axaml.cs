using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using ControlR.Viewer.Avalonia.ViewModels.Dialogs;

namespace ControlR.Viewer.Avalonia.Views.Dialogs;

public partial class TextPromptDialogView : UserControl
{
  public TextPromptDialogView()
  {
    InitializeComponent();
    AttachedToVisualTree += (_, _) => Dispatcher.UIThread.Post(() => PromptTextBox.Focus(), DispatcherPriority.Background);
  }

  private void PromptTextBox_OnKeyDown(object? sender, KeyEventArgs e)
  {
    if (e.Key != Key.Enter || DataContext is not TextPromptDialogViewModel viewModel)
    {
      return;
    }

    if (viewModel.SubmitCommand.CanExecute(null))
    {
      viewModel.SubmitCommand.Execute(null);
      e.Handled = true;
    }
  }
}