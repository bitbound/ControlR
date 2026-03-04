using Avalonia.Controls;
using Avalonia.Controls.Templates;
using ControlR.Viewer.Avalonia.ViewModels.Fakes;

namespace ControlR.Viewer.Avalonia;

public class ViewLocator(Guid viewerInstanceId = default) : IDataTemplate
{

  public Control? Build(object? param)
  {
    try
    {
      if (param is null)
        return null;

      if (param is not IViewModelBase viewModel)
      {
        return new TextBlock { Text = Resources.ViewLocator_InvalidViewModelType };
      }

      var viewType = viewModel.ViewType;

      if (viewType is null)
      {
        return new TextBlock { Text = string.Format(Resources.ViewLocator_ViewNotFound, viewType) };
      }

      if (viewModel is IViewModelBaseFake)
      {
        return Activator.CreateInstance(viewType) as Control;
      }

      if (viewerInstanceId == Guid.Empty)
      {
        return new TextBlock()
        {
          Text = string.Format(Resources.ViewLocator_ViewerInstanceIdEmpty, nameof(IViewModelBaseFake))
        };
      }

      var found = ViewerRegistry.GetService(viewerInstanceId, viewType);
      if (found is not Control control)
      {
        return new TextBlock { Text = string.Format(Resources.ViewLocator_CouldNotCreateView, viewType) };
      }

      control.DataContext = param;
      return control;
    }
    catch (Exception ex)
    {
      return new TextBlock { Text = string.Format(Resources.ViewLocator_ErrorLoadingView, param, ex.Message) };
    }
  }

  public bool Match(object? data)
  {
    return data is IViewModelBase;
  }
}
