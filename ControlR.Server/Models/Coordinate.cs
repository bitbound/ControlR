using ControlR.Server.Interfaces;

namespace ControlR.Server.Models;

public class Coordinate(double latitude, double longitude) : ICoordinate
{
    public double Latitude { get; set; } = latitude;
    public double Longitude { get; set; } = longitude;
}
