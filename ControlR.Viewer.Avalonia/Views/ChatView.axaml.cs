using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace ControlR.Viewer.Avalonia.Views;

public partial class ChatView : UserControl
{
  private IChatViewModel? _viewModel;

  public ChatView()
  {
    InitializeComponent();
    DataContextChanged += HandleDataContextChanged;
  }

  protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
  {
    base.OnAttachedToVisualTree(e);
    UpdateViewModel(DataContext as IChatViewModel);
    ChatTextBox.AddHandler(KeyDownEvent, ChatTextBox_OnKeyDown, RoutingStrategies.Tunnel, true);
    Dispatcher.UIThread.Post(ScrollToMessagesBottom, DispatcherPriority.Background);
  }

  protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
  {
    base.OnDetachedFromVisualTree(e);
    UpdateViewModel(null);
    ChatTextBox.RemoveHandler(KeyDownEvent, ChatTextBox_OnKeyDown);
  }

  private void ChatTextBox_OnKeyDown(object? sender, KeyEventArgs e)
  {
    if (_viewModel is null)
    {
      return;
    }

    if (e.Key == Key.Enter)
    {
      if (e.KeyModifiers.HasFlag(KeyModifiers.Control) || !_viewModel.EnableMultiline)
      {
        _ = _viewModel.SendMessageCommand.ExecuteAsync(null);
        e.Handled = true;
      }
    }
  }

  private void HandleChatMessagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
  {
    if (e.Action == NotifyCollectionChangedAction.Add ||
        e.Action == NotifyCollectionChangedAction.Replace)
    {
      Dispatcher.UIThread.Post(ScrollToMessagesBottom, DispatcherPriority.Background);
    }
  }

  private void HandleDataContextChanged(object? sender, EventArgs e)
  {
    UpdateViewModel(DataContext as IChatViewModel);
  }

  private void ScrollToMessagesBottom()
  {
    MessagesScrollViewer.Offset = new Vector(
      MessagesScrollViewer.Offset.X,
      MessagesScrollViewer.Extent.Height);
  }

  private void UpdateViewModel(IChatViewModel? viewModel)
  {
    if (_viewModel?.ChatMessages is INotifyCollectionChanged previousMessages)
    {
      previousMessages.CollectionChanged -= HandleChatMessagesChanged;
    }

    _viewModel = viewModel;

    if (_viewModel?.ChatMessages is INotifyCollectionChanged currentMessages)
    {
      currentMessages.CollectionChanged += HandleChatMessagesChanged;
    }
  }
}
