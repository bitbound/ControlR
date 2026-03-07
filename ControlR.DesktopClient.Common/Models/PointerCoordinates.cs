namespace ControlR.DesktopClient.Common.Models;

/// <summary>
/// Normalized pointer location plus the selected display metadata.
/// Each input backend is responsible for converting this into the coordinate space
/// required by the underlying platform API.
/// </summary>
public record PointerCoordinates(double NormalizedX, double NormalizedY, DisplayInfo Display);
