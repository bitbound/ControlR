using System.Collections.Immutable;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using ControlR.Assist.ViewModels;
using ControlR.Assist.Views;

namespace ControlR.Assist;

/// <summary>
/// Maps view models to views through an explicit factory table. A reflection-based locator would ask
/// the linker to keep constructors it cannot see a caller for, which is exactly what a trimmed
/// single-file publish removes.
/// </summary>
public sealed class ViewLocator : IDataTemplate
{
  private static readonly ImmutableDictionary<Type, Func<Control>> _viewFactories =
    new Dictionary<Type, Func<Control>>
    {
      [typeof(HomeViewModel)] = () => new HomeView(),
      [typeof(AboutViewModel)] = () => new AboutView()
    }.ToImmutableDictionary();

  public Control? Build(object? param)
  {
    if (param is null)
    {
      return null;
    }

    if (!_viewFactories.TryGetValue(param.GetType(), out var factory))
    {
      return new TextBlock { Text = string.Format(Localization.ViewNotFoundMessage, param.GetType().Name) };
    }

    var control = factory();
    control.DataContext = param;
    return control;
  }

  public bool Match(object? data) => data is ViewModelBase;
}
