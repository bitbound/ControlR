using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using ControlR.Libraries.Api.Contracts.Dtos.HubDtos.PwshCommandCompletions;

namespace ControlR.Viewer.Avalonia.Views;

public partial class TerminalView : UserControl
{
  private bool _applyingCompletion;
  private ITerminalViewModel? _viewModel;

  public TerminalView()
  {
    InitializeComponent();
    DataContextChanged += HandleDataContextChanged;
  }

  protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
  {
    base.OnAttachedToVisualTree(e);
    UpdateViewModel(DataContext as ITerminalViewModel);
    CommandInputTextBox.AddHandler(KeyDownEvent, CommandInputTextBox_OnKeyDown, global::Avalonia.Interactivity.RoutingStrategies.Tunnel, true);
    Dispatcher.UIThread.Post(FocusCommandInput, DispatcherPriority.Background);
    Dispatcher.UIThread.Post(ScrollToEnd, DispatcherPriority.Background);
  }

  protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
  {
    base.OnDetachedFromVisualTree(e);
    CommandInputTextBox.RemoveHandler(KeyDownEvent, CommandInputTextBox_OnKeyDown);
    UpdateViewModel(null);
  }

  private static bool IsCommandCompletionInput(KeyEventArgs args)
  {
    if (args.Key == Key.Tab || args.Key == Key.LeftShift || args.Key == Key.RightShift)
    {
      return true;
    }

    if (args.KeyModifiers.HasFlag(KeyModifiers.Control) && args.Key == Key.Space)
    {
      return true;
    }

    if (args.KeyModifiers.HasFlag(KeyModifiers.Control) && args.Key == Key.OemPeriod)
    {
      return true;
    }

    return false;
  }

  private async Task ApplySelectedCompletion()
  {
    if (_applyingCompletion || _viewModel is null || CompletionListBox.SelectedItem is not PwshCompletionMatch match)
    {
      return;
    }

    try
    {
      _applyingCompletion = true;
      CompletionListBox.SelectedItem = null;
      await _viewModel.ApplyCompletion(match);
      SetCaretToEnd();
      FocusCommandInput();
    }
    finally
    {
      _applyingCompletion = false;
    }
  }

  private void CommandInputTextBox_OnKeyDown(object? sender, KeyEventArgs e)
  {
    if (_viewModel is null)
    {
      return;
    }

    if (_viewModel.HasCompletions)
    {
      if (e.Key == Key.Up)
      {
        e.Handled = true;
        FocusCompletionList(false);
        return;
      }

      if (e.Key == Key.Down)
      {
        e.Handled = true;
        FocusCompletionList(true);
        return;
      }

      if (e.Key == Key.Enter || e.Key == Key.Tab)
      {
        e.Handled = true;
        ApplySelectedCompletion().Forget();
        return;
      }
    }

    if (e.Key == Key.Escape && !e.KeyModifiers.HasFlag(KeyModifiers.Control) &&
        !e.KeyModifiers.HasFlag(KeyModifiers.Shift) && !e.KeyModifiers.HasFlag(KeyModifiers.Alt))
    {
      e.Handled = true;
      HandleEscape().Forget();
      return;
    }

    if (!IsCommandCompletionInput(e))
    {
      _viewModel.ResetCompletionState();
    }

    if (!_viewModel.EnableMultiline && e.Key == Key.Up)
    {
      SetCommandInputText(_viewModel.GetTerminalHistory(false));
      e.Handled = true;
      return;
    }

    if (!_viewModel.EnableMultiline && e.Key == Key.Down)
    {
      SetCommandInputText(_viewModel.GetTerminalHistory(true));
      e.Handled = true;
      return;
    }

    if (e.Key == Key.Enter)
    {
      if (e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Shift))
      {
        return;
      }

      e.Handled = true;
      HandleEnter().Forget();
      return;
    }

    if (e.Key == Key.Tab)
    {
      e.Handled = true;
      HandleTab(!e.KeyModifiers.HasFlag(KeyModifiers.Shift)).Forget();
      return;
    }

    if (e.KeyModifiers.HasFlag(KeyModifiers.Control) &&
        (e.Key == Key.Space || e.Key == Key.OemPeriod))
    {
      e.Handled = true;
      HandleShowAllCompletions().Forget();
      return;
    }
  }

  private async void CompletionListBox_OnDoubleTapped(object? sender, TappedEventArgs e)
  {
    await ApplySelectedCompletion();
  }

  private async void CompletionListBox_OnKeyDown(object? sender, KeyEventArgs e)
  {
    if (_viewModel is null)
    {
      return;
    }

    if (e.Key == Key.Enter || e.Key == Key.Tab)
    {
      e.Handled = true;
      await ApplySelectedCompletion();
      return;
    }

    if (e.Key == Key.Escape)
    {
      e.Handled = true;
      await HandleEscape();
    }
  }

  private void FocusCommandInput()
  {
    CommandInputTextBox.Focus();
  }

  private void FocusCompletionList(bool moveForward)
  {
    if (CompletionListBox.ItemCount == 0)
    {
      FocusCommandInput();
      return;
    }

    if (CompletionListBox.SelectedIndex < 0)
    {
      CompletionListBox.SelectedIndex = moveForward ? 0 : CompletionListBox.ItemCount - 1;
    }
    else
    {
      var delta = moveForward ? 1 : -1;
      CompletionListBox.SelectedIndex = Math.Clamp(
        CompletionListBox.SelectedIndex + delta,
        0,
        CompletionListBox.ItemCount - 1);
    }

    CompletionListBox.Focus();

    if (CompletionListBox.SelectedItem is not null)
    {
      CompletionListBox.ScrollIntoView(CompletionListBox.SelectedItem);
    }
  }

  private void HandleDataContextChanged(object? sender, EventArgs e)
  {
    UpdateViewModel(DataContext as ITerminalViewModel);
  }

  private async Task HandleEnter()
  {
    if (_viewModel is null)
    {
      return;
    }

    await _viewModel.HandleEnterKeyInput();
    SetCaretToEnd();
    FocusCommandInput();
  }

  private async Task HandleEscape()
  {
    if (_viewModel is null)
    {
      return;
    }

    CompletionListBox.SelectedItem = null;
    _ = await _viewModel.TryHandleEscape();
    FocusCommandInput();
  }

  private void HandleOutputLinesChanged(object? sender, NotifyCollectionChangedEventArgs e)
  {
    Dispatcher.UIThread.Post(ScrollToEnd, DispatcherPriority.Background);
  }

  private async Task HandleShowAllCompletions()
  {
    if (_viewModel is null)
    {
      return;
    }

    await _viewModel.GetAllCompletions(CommandInputTextBox.CaretIndex);
    FocusCompletionList(true);
  }

  private async Task HandleTab(bool forward)
  {
    if (_viewModel is null)
    {
      return;
    }

    await _viewModel.GetNextCompletion(forward, CommandInputTextBox.CaretIndex);
    SetCaretToEnd();
    FocusCommandInput();
  }

  private void KeyboardShortcutsButton_OnClick(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
  {
    _viewModel?.ShowKeyboardShortcuts();
  }

  private void ScrollToEnd()
  {
    OutputScrollViewer.Offset = new Vector(OutputScrollViewer.Offset.X, OutputScrollViewer.Extent.Height);
  }

  private void SetCaretToEnd()
  {
    CommandInputTextBox.CaretIndex = CommandInputTextBox.Text?.Length ?? 0;
  }

  private void SetCommandInputText(string text)
  {
    if (_viewModel is null)
    {
      return;
    }

    _viewModel.SetCommandInputText(text);
    SetCaretToEnd();
  }

  private void UpdateViewModel(ITerminalViewModel? viewModel)
  {
    if (_viewModel?.OutputLines is INotifyCollectionChanged previousOutputLines)
    {
      previousOutputLines.CollectionChanged -= HandleOutputLinesChanged;
    }

    _viewModel = viewModel;

    if (_viewModel?.OutputLines is INotifyCollectionChanged currentOutputLines)
    {
      currentOutputLines.CollectionChanged += HandleOutputLinesChanged;
    }
  }
}