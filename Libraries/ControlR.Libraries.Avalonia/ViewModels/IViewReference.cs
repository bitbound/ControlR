namespace ControlR.Libraries.Avalonia.ViewModels;

public interface IViewReference<TView>
{
  public Type ViewType { get; }
}