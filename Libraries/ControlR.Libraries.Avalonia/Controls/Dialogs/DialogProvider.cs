using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Threading;
using ControlR.Libraries.Avalonia.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace ControlR.Libraries.Avalonia.Controls.Dialogs;

public interface IDialogProvider : INotifyPropertyChanged
{
  object? Content { get; }
  bool IsVisible { get; }
  double MaxHeight { get; }
  double MaxWidth { get; }
  string? Title { get; }

  void Close();
  void Show<TViewModel, TView>(
    string title,
    TViewModel viewModel,
    double maxWidth = double.PositiveInfinity,
    double maxHeight = double.PositiveInfinity)
    where TViewModel : IViewReference<TView>;
}

internal sealed class DialogProvider(IServiceProvider serviceProvider) : ObservableObject, IDialogProvider
{
  private readonly IServiceProvider _serviceProvider = serviceProvider;
  private object? _content;
  private bool _isVisible;
  private double _maxHeight = double.PositiveInfinity;
  private double _maxWidth = double.PositiveInfinity;
  private string? _title;

  public object? Content
  {
    get => _content;
    private set => SetProperty(ref _content, value);
  }
  public bool IsVisible
  {
    get => _isVisible;
    private set => SetProperty(ref _isVisible, value);
  }
  public double MaxHeight
  {
    get => _maxHeight;
    private set => SetProperty(ref _maxHeight, value);
  }
  public double MaxWidth
  {
    get => _maxWidth;
    private set => SetProperty(ref _maxWidth, value);
  }
  public string? Title
  {
    get => _title;
    private set => SetProperty(ref _title, value);
  }

  public void Close()
  {
    void CloseCore()
    {
      DisposeContentIfNeeded(Content);
      Content = null;
      Title = null;
      MaxWidth = double.PositiveInfinity;
      MaxHeight = double.PositiveInfinity;
      IsVisible = false;
    }

    if (Dispatcher.UIThread.CheckAccess())
    {
      CloseCore();
      return;
    }

    Dispatcher.UIThread.Post(CloseCore);
  }

  public void Show<TViewModel, TView>(
    string title,
    TViewModel viewModel,
    double maxWidth = double.PositiveInfinity,
    double maxHeight = double.PositiveInfinity)
    where TViewModel : IViewReference<TView>
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(title);
    ArgumentNullException.ThrowIfNull(viewModel);

    var view = _serviceProvider.GetRequiredService(viewModel.ViewType);
    if (view is not ContentControl control)
    {
      throw new InvalidOperationException("View type must be a ContentControl.");
    }

    control.DataContext = viewModel;

    if (Dispatcher.UIThread.CheckAccess())
    {
      ShowCore(title, control, maxWidth, maxHeight);
      return;
    }

    Dispatcher.UIThread.Post(() => ShowCore(title, control, maxWidth, maxHeight));
  }


  private static void DisposeContentIfNeeded(object? value)
  {
    if (value is IDisposable disposable)
    {
      disposable.Dispose();
    }
  }

   private void ShowCore(string title, ContentControl control, double maxWidth, double maxHeight)
  {
    DisposeContentIfNeeded(Content);
    Title = title;
    MaxWidth = maxWidth;
    MaxHeight = maxHeight;
    Content = control;
    IsVisible = true;
  }
}
