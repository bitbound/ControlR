using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ControlR.Libraries.Avalonia.ViewModels;

internal class ObservableObject : INotifyPropertyChanged
{
  public event PropertyChangedEventHandler? PropertyChanged;

  protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
  {
    if (EqualityComparer<T>.Default.Equals(field, value))
    {
      return false;
    }

    field = value;
    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    return true;
  }
}