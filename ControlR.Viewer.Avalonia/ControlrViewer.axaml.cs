using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Avalonia.Layout;
using ControlR.Viewer.Avalonia.ViewModels.Fakes;

namespace ControlR.Viewer.Avalonia;

public partial class ControlrViewer : UserControl
{
  public static readonly StyledProperty<Guid> InstanceIdProperty =
    AvaloniaProperty.Register<ControlrViewer, Guid>(nameof(InstanceId));
  public static readonly StyledProperty<ControlrViewerOptions?> OptionsProperty =
    AvaloniaProperty.Register<ControlrViewer, ControlrViewerOptions?>(nameof(Options));

  private readonly Lock _intializeLock = new();
  private readonly IDisposable _isVisibleSubscription;

  private bool _isInitialized;
  private IServiceProvider? _serviceProvider;

  public ControlrViewer()
  {
    InitializeComponent();

    // Generate unique instance ID.
    InstanceId = Guid.NewGuid();

    _isVisibleSubscription = this
      .GetObservable(IsVisibleProperty)
      .Subscribe(HandleIsVisibleChanged);
  }

  public Guid InstanceId
  {
    get => GetValue(InstanceIdProperty);
    private set => SetValue(InstanceIdProperty, value);
  }
  public ControlrViewerOptions? Options
  {
    get => GetValue(OptionsProperty);
    set => SetValue(OptionsProperty, value);
  }

  /// <summary>
  /// Get a required service from this viewer instance's service provider.
  /// </summary>
  public T GetRequiredService<T>() where T : notnull
  {
    if (_serviceProvider is null)
    {
      throw new InvalidOperationException(Assets.Resources.ControlrViewer_ServiceProviderNotInitialized);
    }
    return _serviceProvider.GetRequiredService<T>();
  }

  /// <summary>
  /// Get a service from this viewer instance's service provider.
  /// </summary>
  public T? GetService<T>() where T : class
  {
    return _serviceProvider?.GetService<T>();
  }

  protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
  {
    base.OnApplyTemplate(e);

    if (Design.IsDesignMode)
    {
      var viewerShell = new ViewerShell()
      {
        DataContext = new ViewerShellViewModelFake()
      };
      viewerShell.DialogHost.IsVisible = false;
      viewerShell.SnackbarHost.IsVisible = false;
      Content = viewerShell;
      return;
    }

    if (Options is null)
    {
      SetErrorContent(Assets.Resources.ControlrViewer_OptionsNotSet);
      return;
    }

    if (Options.BaseUrl is null)
    {
      SetErrorContent(Assets.Resources.ControlrViewer_BaseUrlRequired);
      return;
    }

    if (Options.DeviceId == Guid.Empty)
    {
      SetErrorContent(Assets.Resources.ControlrViewer_DeviceIdRequired);
      return;
    }

    if (string.IsNullOrWhiteSpace(Options.PersonalAccessToken))
    {
      SetErrorContent(Assets.Resources.ControlrViewer_PatRequired);
      return;
    }

    if (IsVisible)
    {
      InitializeServices();
    }
  }

  protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
  {
    base.OnDetachedFromVisualTree(e);

    // Unregister from the global registry
    ViewerRegistry.Unregister(InstanceId);

    try
    {
      _isVisibleSubscription.Dispose();

      if (_serviceProvider is IAsyncDisposable asyncDisposable)
      {
        asyncDisposable.DisposeAsync().Forget();
      }
      else if (_serviceProvider is IDisposable disposable)
      {
        disposable.Dispose();
      }
    }
    catch
    {
      // We tried.
    }
  }

  private void HandleIsVisibleChanged(bool obj)
  {
    if (obj && IsLoaded && !_isInitialized)
    {
      InitializeServices();
    }
  }

  private void InitializeServices()
  {
    using var locker = _intializeLock.EnterScope();

    if (_isInitialized)
    {
      return;
    }

    if (Design.IsDesignMode)
    {
      return;
    }

    try
    {
      if (Options is null)
      {
        SetErrorContent(Assets.Resources.ControlrViewer_OptionsNotSet);
        return;
      }

      if (TopLevel.GetTopLevel(this) is not { } topLevel)
      {
        SetErrorContent(Assets.Resources.ControlrViewer_TopLevelMissing);
        return;
      }

      if (topLevel.Clipboard is not { } clipboard)
      {
        SetErrorContent(Assets.Resources.ControlrViewer_ClipboardMissing);
        return;
      }

      // Build the service provider for this instance
      _serviceProvider = ViewerServiceBuilder.BuildServiceProvider(Options, InstanceId, clipboard);

      // Register this instance in the global registry
      ViewerRegistry.Register(InstanceId, this, _serviceProvider);

      // Create and set the ViewerShell
      Dispatcher.UIThread.Post(() =>
      {
        var viewerShell = _serviceProvider.GetRequiredService<ViewerShell>();
        Content = viewerShell;
      });
    }
    catch (Exception ex)
    {
      // Log or handle initialization error
      SetErrorContent(string.Format(Assets.Resources.ControlrViewer_InitializationError, ex.Message));
    }
    finally
    {
      _isInitialized = true;
    }
  }

  private void SetErrorContent(string message)
  {
    var textBlock = new TextBlock
    {
      Text = message,
      Foreground = Brushes.Red,
      Margin = new Thickness(0, 40, 0, 0),
      FontSize = 20,
      HorizontalAlignment = HorizontalAlignment.Center,
      VerticalAlignment = VerticalAlignment.Top,
      TextWrapping = TextWrapping.Wrap
    };
    Content = textBlock;
  }
}
