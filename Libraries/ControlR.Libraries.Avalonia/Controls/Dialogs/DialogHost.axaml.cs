using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace ControlR.Libraries.Avalonia.Controls.Dialogs;

public partial class DialogHost : UserControl
{
  public static readonly StyledProperty<IDialogProvider?> DialogProviderProperty =
    AvaloniaProperty.Register<DialogHost, IDialogProvider?>(nameof(DialogProvider));

  public DialogHost()
  {
    DataContext = this;
    InitializeComponent();
    var closeButton = this.FindControl<Button>("CloseDialogButton");
    if (closeButton is not null)
    {
      closeButton.Click += OnCloseClicked;
    }
  }

  public IDialogProvider? DialogProvider
  {
    get => GetValue(DialogProviderProperty);
    set => SetValue(DialogProviderProperty, value);
  }

  private void OnCloseClicked(object? sender, RoutedEventArgs e)
  {
    DialogProvider?.Close();
  }
}
