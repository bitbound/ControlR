using System.ComponentModel;

namespace ControlR.Libraries.Avalonia.Controls.Snackbar;

public sealed class SnackbarMessage(
  Guid id,
  string message,
  SnackbarSeverity severity) : INotifyPropertyChanged
{
  private double _opacity;

  public event PropertyChangedEventHandler? PropertyChanged;

  public Guid Id { get; } = id;
  public string Message { get; } = message;
  public double Opacity
  {
    get => _opacity;
    set
    {
      if (Math.Abs(_opacity - value) < double.Epsilon)
      {
        return;
      }

      _opacity = value;
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Opacity)));
    }
  }
  public SnackbarSeverity Severity { get; } = severity;
}