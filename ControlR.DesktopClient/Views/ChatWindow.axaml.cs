using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ControlR.DesktopClient.ViewModels;

namespace ControlR.DesktopClient;

public partial class ChatWindow : Window
{
    public ChatWindow()
    {
        DataContext = StaticServiceProvider.Instance.GetRequiredService<IChatWindowViewModel>();
        InitializeComponent();
    }

    public IChatWindowViewModel ViewModel =>
        DataContext as IChatWindowViewModel
        ?? throw new InvalidOperationException("DataContext is not set.");

  protected override void OnClosed(EventArgs e)
  {
    ViewModel.HandleChatWindowClosed();
    base.OnClosed(e);
  }
}