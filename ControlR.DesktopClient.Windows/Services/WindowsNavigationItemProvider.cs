using ControlR.DesktopClient.Common;
using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.DesktopClient.Common.ViewModels;

namespace ControlR.DesktopClient.Windows.Services;

public class WindowsNavigationItemProvider : INavigationItemProvider
{
  public IEnumerable<NavigationItemDescriptor> GetNavigationItems()
  {
    return
    [
      new(typeof(IPermissionsViewModel), "shield_keyhole_regular", Localization.Permissions, 100)
    ];
  }
}
