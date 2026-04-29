using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Avalonia.Layout;
using ControlR.Viewer.Avalonia.ViewModels.Fakes;
using ControlR.Viewer.Avalonia.Services;
using ControlR.Viewer.Avalonia.Services.Navigation;
using System.ComponentModel;

namespace ControlR.Viewer.Avalonia;

public partial class ControlrViewer : UserControl
{
  public static readonly StyledProperty<Guid> InstanceIdProperty =
    AvaloniaProperty.Register<ControlrViewer, Guid>(nameof(InstanceId));
  public static readonly StyledProperty<ControlrViewerOptions?> OptionsProperty =
    AvaloniaProperty.Register<ControlrViewer, ControlrViewerOptions?>(nameof(Options));
  public static readonly StyledProperty<ViewerPage> PageProperty =
    AvaloniaProperty.Register<ControlrViewer, ViewerPage>(nameof(Page), ViewerPage.None);

  private readonly Lock _intializeLock = new();
  private readonly IDisposable _isVisibleSubscription;
  private readonly IDisposable _pageSubscription;

  private ViewerInstanceInfo? _instanceInfo;
  private bool _isInitialized;
  private bool _isShellConnected;
  private ViewerPage _pendingPage;
  private IServiceProvider? _serviceProvider;
  private IViewerShellViewModel? _shellViewModel;

  public ControlrViewer()
  {
    InitializeComponent();

    // Generate unique instance ID.
    InstanceId = Guid.NewGuid();
    _pendingPage = Page;

    _isVisibleSubscription = this
      .GetObservable(IsVisibleProperty)
      .Subscribe(HandleIsVisibleChanged);
    _pageSubscription = this
      .GetObservable(PageProperty)
      .Subscribe(HandlePageChanged);
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
  public ViewerPage Page
  {
    get => GetValue(PageProperty);
    set => SetValue(PageProperty, value);
  }

  /// <summary>
  /// Get the public-facing instance information for this viewer.
  /// </summary>
  public ViewerInstanceInfo GetInstanceInfo()
  {
    if (_instanceInfo is null)
    {
      throw new InvalidOperationException(Assets.Resources.ControlrViewer_ServiceProviderNotInitialized);
    }

    return _instanceInfo;
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
      _instanceInfo = null;
      _pageSubscription.Dispose();
      _isVisibleSubscription.Dispose();

      if (_shellViewModel is not null)
      {
        _shellViewModel.PropertyChanged -= HandleShellViewModelPropertyChanged;
      }

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

  private async Task ApplyPendingPage()
  {
    if (_serviceProvider is null)
    {
      return;
    }

    if (_pendingPage != ViewerPage.None && !_isShellConnected)
    {
      return;
    }

    if (!Dispatcher.UIThread.CheckAccess())
    {
      await Dispatcher.UIThread.InvokeAsync(ApplyPendingPage);
      return;
    }

    var navigationProvider = _serviceProvider.GetRequiredService<INavigationProvider>();
    if (_pendingPage == ViewerPage.None && navigationProvider.ActivePage == ViewerPage.None)
    {
      return;
    }

    if (_pendingPage == navigationProvider.ActivePage)
    {
      return;
    }

    var navigator = _serviceProvider.GetRequiredService<INavigator>();
    var result = await navigator.NavigateTo(_pendingPage);
    if (!result.IsSuccess)
    {
      SetErrorContent(result.Reason);
    }
  }

  private void HandleIsVisibleChanged(bool obj)
  {
    if (obj && IsLoaded && !_isInitialized)
    {
      InitializeServices();
    }
  }

  private void HandlePageChanged(ViewerPage page)
  {
    _pendingPage = page;
    ApplyPendingPage().Forget();
  }

  private void HandleShellViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
  {
    if (e.PropertyName != nameof(IViewerShellViewModel.ConnectionState) || _shellViewModel is null)
    {
      return;
    }

    _isShellConnected = _shellViewModel.IsConnected;
    ApplyPendingPage().Forget();
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
      _shellViewModel = _serviceProvider.GetRequiredService<IViewerShellViewModel>();
      _shellViewModel.PropertyChanged += HandleShellViewModelPropertyChanged;

      // Register this instance in the global registry
      _instanceInfo = ViewerRegistry.Register(InstanceId, this, _serviceProvider);

      // Create and set the ViewerShell
      Dispatcher.UIThread.Post(() =>
      {
        var viewerShell = _serviceProvider.GetRequiredService<ViewerShell>();
        Content = viewerShell;
        ApplyPendingPage().Forget();
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
