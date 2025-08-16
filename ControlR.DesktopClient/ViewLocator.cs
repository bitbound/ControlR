using Avalonia.Controls;
using Avalonia.Controls.Templates;
using ControlR.DesktopClient.ViewModels;

namespace ControlR.DesktopClient;
public class ViewLocator : IDataTemplate
{

  public Control? Build(object? param)
  {
    if (param is null)
      return null;

    var name = param.GetType().FullName!.Replace("ViewModel", "View", StringComparison.Ordinal);
    if (name.EndsWith("Fake"))
    {
      name = name
        .Replace(".Fakes.", ".")
        .TrimEnd("Fake".ToCharArray());

    }
    var type = Type.GetType(name);

    if (type != null)
    {
      return (Control)StaticServiceProvider.Instance.GetRequiredService(type);
    }

    return new TextBlock { Text = "Not Found: " + name };
  }

  public bool Match(object? data)
  {
    return data is ViewModelBase;
  }
}
