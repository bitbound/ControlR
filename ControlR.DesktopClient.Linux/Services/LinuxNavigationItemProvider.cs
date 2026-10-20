using ControlR.DesktopClient.Common;
using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.DesktopClient.Common.ViewModels;
using ControlR.DesktopClient.ViewModels.Linux;

namespace ControlR.DesktopClient.Linux.Services;

public class LinuxNavigationItemProvider(IDesktopEnvironmentDetector desktopEnvironmentDetector) : INavigationItemProvider
{
  private readonly IDesktopEnvironmentDetector _desktopEnvironmentDetector = desktopEnvironmentDetector;

  public IEnumerable<NavigationItemDescriptor> GetNavigationItems()
  {
    var viewModelType = _desktopEnvironmentDetector.IsWayland()
      ? typeof(IPermissionsViewModelWayland)
      : typeof(IPermissionsViewModel);

    return
    [
      new(viewModelType, "shield_keyhole_regular", Localization.Permissions, 100)
    ];
  }
}
