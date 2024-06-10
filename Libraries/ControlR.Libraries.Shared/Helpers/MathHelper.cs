namespace ControlR.Libraries.Shared.Helpers;
public static class MathHelper
{
    public static double GetDistanceBetween(double point1X, double point1Y, double point2X, double point2Y)
    {
        return Math.Sqrt(Math.Pow(point1X - point2X, 2) +
            Math.Pow(point1Y - point2Y, 2));
    }
}
