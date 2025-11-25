using System.Drawing;

namespace ControlR.DesktopClient.Common.Models;

public record PointerCoordinates(double PercentX, double PercentY, Point AbsolutePoint, DisplayInfo Display);
