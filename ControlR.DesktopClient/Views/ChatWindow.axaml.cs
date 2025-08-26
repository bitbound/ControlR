using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using ControlR.DesktopClient.ViewModels;

namespace ControlR.DesktopClient;

public partial class ChatWindow : Window
{
  public ChatWindow()
  {
    DataContext = StaticServiceProvider.Instance.GetRequiredService<IChatWindowViewModel>();
    InitializeComponent();
    ViewModel.Messages.CollectionChanged += HandleMessagesChanged;
  }

  public IChatWindowViewModel ViewModel =>
      DataContext as IChatWindowViewModel
      ?? throw new InvalidOperationException("DataContext is not set.");

  protected override async void OnClosed(EventArgs e)
  {
    await ViewModel.HandleChatWindowClosed();
    ViewModel.Messages.CollectionChanged -= HandleMessagesChanged;
    base.OnClosed(e);
  }

  private void HandleMessagesChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
  {
    // Scroll to the bottom when a new message is added
    _messagesScrollViewer.ScrollToEnd();
  }

  private async void OnTextBoxKeyDown(object? sender, KeyEventArgs e)
  {
    if (e.Key == Key.Enter)
    {
      // If Shift is pressed, allow default behavior (new line)
      if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
      {
        ViewModel.NewMessage += Environment.NewLine;
        _chatInputTextBox.SelectionStart = ViewModel.NewMessage.Length;
        _chatInputTextBox.SelectionEnd = ViewModel.NewMessage.Length;
        return; // Let the TextBox handle Shift+Enter for new line
      }

      // If only Enter is pressed, send the message
      e.Handled = true;
      await ViewModel.SendMessage();
    }
  }
}