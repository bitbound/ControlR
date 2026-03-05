using ControlR.Libraries.Shared.Primitives;

namespace ControlR.DesktopClient.Common.Models;

/// <summary>
/// Normalized pointer location plus a platform-specific physical coordinate.
/// For absolute motion this is typically a physical pixel position; for relative motion
/// it may represent deltas depending on platform/input backend.
/// </summary>
public record PointerCoordinates(double NormalizedX, double NormalizedY, PhysicalPoint PhysicalPoint, DisplayInfo Display);
