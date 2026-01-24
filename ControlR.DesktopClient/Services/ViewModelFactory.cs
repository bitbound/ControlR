using ControlR.DesktopClient.ViewModels;

namespace ControlR.DesktopClient.Services;

public interface IViewModelFactory
{
  INavItemViewModel CreateNavItem<TDestination>(string iconKey, string label) where TDestination : IViewModelBase;
  INavItemViewModel CreateNavItem(Type destinationType, string iconKey, string label);
}

public class ViewModelFactory(IServiceProvider serviceProvider) : IViewModelFactory
{
  public INavItemViewModel CreateNavItem<TDestination>(string iconKey, string label) where TDestination : IViewModelBase
  {
    return ActivatorUtilities.CreateInstance<NavItemViewModel<TDestination>>(serviceProvider, iconKey, label);
  }

  public INavItemViewModel CreateNavItem(Type destinationType, string iconKey, string label)
  {
    var navItemType = typeof(NavItemViewModel<>).MakeGenericType(destinationType);
    return (INavItemViewModel)ActivatorUtilities.CreateInstance(serviceProvider, navItemType, iconKey, label);
  }
}