using ControlR.Server.Interfaces;

namespace ControlR.Libraries.Shared.Helpers;

public static class CoordinateHelper
{
    public static T FindClosestCoordinate<T>(ICoordinate target, IReadOnlyList<T> coordinates)
        where T : ICoordinate
    {
        if (coordinates.Count == 0)
        {
            throw new ArgumentException("Hosts can't be empty.");
        }

        double minDistance = double.MaxValue;
        var closestHost = coordinates[0];

        foreach (var host in coordinates)
        {
            double distance = CalculateDistance(host, target);
            if (distance < minDistance)
            {
                minDistance = distance;
                closestHost = host;
            }
        }

        return closestHost;
    }

    private static double CalculateDistance(ICoordinate a, ICoordinate b)
    {
        var dx = a.Longitude - b.Longitude;
        var dy = a.Latitude - b.Latitude;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}