namespace ControlR.Libraries.Shared.Primitives;

public readonly record struct LogicalRect(double X, double Y, double Width, double Height)
{
  public bool Contains(LogicalPoint point)
  {
    var right = X + Width;
    var bottom = Y + Height;

    return point.X >= X &&
           point.Y >= Y &&
           point.X < right &&
           point.Y < bottom;
  }
}
