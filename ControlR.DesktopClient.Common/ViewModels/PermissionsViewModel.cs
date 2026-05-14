using ControlR.DesktopClient.Common.ViewModelInterfaces;
using ControlR.DesktopClient.Common.ViewModels;
using ControlR.DesktopClient.Views;

namespace ControlR.DesktopClient.ViewModels;

public interface IPermissionsViewModel : IViewModelBase
{
}

public class PermissionsViewModel : ViewModelBase<PermissionsView>, IPermissionsViewModel
{
}
