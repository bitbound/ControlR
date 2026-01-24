using Avalonia.Controls;
using Avalonia.Controls.Templates;

namespace ControlR.DesktopClient;
public class ViewLocator : IDataTemplate
{

  public Control? Build(object? param)
  {
    if (param is null)
      return null;

    if (param is not IViewModelBase viewModel)
    {
      return new TextBlock { Text = "Invalid view model type" };
    }

    var viewType = viewModel.ViewType;

    if (viewType is null)
    {
      return new TextBlock { Text = $"View not found: {viewType}" };
    }
    var found = ActivatorUtilities.GetServiceOrCreateInstance(StaticServiceProvider.Instance, viewType);
    if (found is not Control control)
    {
      return new TextBlock { Text = $"Could not create view: {viewType}" };
    }

    control.DataContext = param;
    return control;
  }

  public bool Match(object? data)
  {
    return data is IViewModelBase;
  }
}
