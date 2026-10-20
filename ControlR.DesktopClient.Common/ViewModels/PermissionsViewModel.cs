using ControlR.DesktopClient.Common.ViewModelInterfaces;
using ControlR.DesktopClient.Views;

namespace ControlR.DesktopClient.Common.ViewModels;

public interface IPermissionsViewModel : IViewModelBase
{
}

public class PermissionsViewModel : ViewModelBase<PermissionsView>, IPermissionsViewModel
{
}
