namespace ControlR.Libraries.Avalonia.Controls.Snackbar;

public sealed class SnackbarOptions
{
  public TimeSpan FadeDuration { get; set; } = TimeSpan.FromSeconds(0.5);
  public bool NewestOnTop { get; set; }
  public SnackbarPosition Position { get; set; } = SnackbarPosition.BottomRight;
  public TimeSpan VisibleStateDuration { get; set; } = TimeSpan.FromSeconds(2);
}

public enum SnackbarPosition
{
  TopLeft,
  TopCenter,
  TopRight,
  BottomLeft,
  BottomCenter,
  BottomRight,
}