using Avalonia.Controls;
using Avalonia.Controls.Templates;
using ControlR.DesktopClient.Common;

namespace ControlR.DesktopClient;
public class ViewLocator : IDataTemplate
{

  public Control? Build(object? param)
  {
    if (param is null)
      return null;

    if (param is not IViewModelBase viewModel)
    {
      return new TextBlock { Text = Localization.InvalidViewModelTypeMessage };
    }

    var viewType = viewModel.ViewType;

    if (viewType is null)
    {
      return new TextBlock { Text = string.Format(Localization.ViewNotFoundMessage, "null") };
    }
    var found = ActivatorUtilities.GetServiceOrCreateInstance(StaticServiceProvider.Instance, viewType);
    if (found is not Control control)
    {
      return new TextBlock { Text = string.Format(Localization.CouldNotCreateViewMessage, viewType?.GetType()) };
    }

    control.DataContext = param;
    return control;
  }

  public bool Match(object? data)
  {
    return data is IViewModelBase;
  }
}
