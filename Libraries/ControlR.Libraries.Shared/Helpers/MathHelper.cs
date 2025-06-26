namespace ControlR.Libraries.Shared.Helpers;

public static class MathHelper
{
  public static double GetDistanceBetween(double x1, double y1, double x2, double y2)
  {
    var dx = x1 - x2;
    var dy = y1 - y2;
    return Math.Sqrt(dx * dx + dy * dy);
  }
}
