using Microsoft.Extensions.DependencyInjection;

namespace ControlR.DesktopClient.Linux.XdgPortal;

public interface IXdgDesktopPortalFactory
{
  IXdgDesktopPortal CreateNew(bool replaceDefault = false);
  IXdgDesktopPortal GetOrCreateDefault();
}

public class XdgDesktopPortalFactory(IServiceProvider serviceProvider) : IXdgDesktopPortalFactory
{
  private readonly Lock _defaultInstanceLock = new();
  private readonly IServiceProvider _serviceProvider = serviceProvider;
  private IXdgDesktopPortal? _defaultPortal;
  

  public IXdgDesktopPortal CreateNew(bool replaceDefault = false)
  {
    using var lockScope = _defaultInstanceLock.EnterScope();
    if (replaceDefault)
    {
      _defaultPortal = ActivatorUtilities.CreateInstance<XdgDesktopPortal>(_serviceProvider);
      return _defaultPortal;
    }
    return ActivatorUtilities.CreateInstance<XdgDesktopPortal>(_serviceProvider);
  }

  public IXdgDesktopPortal GetOrCreateDefault()
  {
    using var lockScope = _defaultInstanceLock.EnterScope();
    _defaultPortal ??= ActivatorUtilities.CreateInstance<XdgDesktopPortal>(_serviceProvider);
    return _defaultPortal;
  }
}