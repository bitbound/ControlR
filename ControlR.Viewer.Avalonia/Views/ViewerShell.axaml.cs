using Avalonia.Controls;
using ControlR.Libraries.Avalonia.Controls.Dialogs;
using ControlR.Libraries.Avalonia.Controls.Snackbar;

namespace ControlR.Viewer.Avalonia.Views;

public partial class ViewerShell : UserControl
{
  public ViewerShell()
  {
    InitializeComponent();
    if (Design.IsDesignMode)
    {
      DataTemplates.Add(new ViewLocator());
    }
  }

  public ViewerShell(
    IViewerShellViewModel viewModel,
    IInstanceIdProvider instanceIdProvider,
    IDialogProvider dialogProvider,
    ISnackbar snackbar) : this()
  {

    DataTemplates.Add(new ViewLocator(instanceIdProvider.InstanceId));
    DialogHost.DialogProvider = dialogProvider;
    SnackbarHost.Snackbar = snackbar;
    DataContext = viewModel;
    viewModel.Initialize();
  }
}
