namespace ControlR.Libraries.Shared.Primitives;

public readonly record struct LogicalPoint(double X, double Y)
{
  public LogicalPoint Clamp(LogicalRect bounds)
  {
    var maxX = bounds.X + Math.Max(0, bounds.Width);
    var maxY = bounds.Y + Math.Max(0, bounds.Height);

    var clampedX = Math.Clamp(X, bounds.X, maxX);
    var clampedY = Math.Clamp(Y, bounds.Y, maxY);

    return new LogicalPoint(clampedX, clampedY);
  }
}
