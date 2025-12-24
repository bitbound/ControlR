using ControlR.DesktopClient.Common.ServiceInterfaces;
using Microsoft.Extensions.DependencyInjection;

namespace ControlR.DesktopClient.Common.Services;

public interface IScreenGrabberFactory
{
  IScreenGrabber CreateNew(bool replaceDefault = false);
  IScreenGrabber GetOrCreateDefault();
}

public class ScreenGrabberFactory<TImplementation>(IServiceProvider serviceProvider) : IScreenGrabberFactory
  where TImplementation : class, IScreenGrabber
{
  private readonly Lock _defaultInstanceLock = new();
  private readonly IServiceProvider _serviceProvider = serviceProvider;
  private IScreenGrabber? _defaultInstance;

  public IScreenGrabber CreateNew(bool replaceDefault = false)
  {
    using var lockScope = _defaultInstanceLock.EnterScope();
    if (replaceDefault)
    {
      _defaultInstance = ActivatorUtilities.CreateInstance<TImplementation>(_serviceProvider);
      return _defaultInstance;
    }
    return ActivatorUtilities.CreateInstance<TImplementation>(_serviceProvider);
  }

  public IScreenGrabber GetOrCreateDefault()
  {
    using var lockScope = _defaultInstanceLock.EnterScope();
    _defaultInstance ??= ActivatorUtilities.CreateInstance<TImplementation>(_serviceProvider);
    return _defaultInstance;
  }
}
