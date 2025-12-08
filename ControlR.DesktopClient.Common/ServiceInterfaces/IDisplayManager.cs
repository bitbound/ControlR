using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using ControlR.DesktopClient.Common.Models;
using ControlR.Libraries.Shared.Dtos.HubDtos;
using ControlR.Libraries.Shared.Primitives;

namespace ControlR.DesktopClient.Common.ServiceInterfaces;

public interface IDisplayManager
{
  Task<Point> ConvertPercentageLocationToAbsolute(string displayName, double percentX, double percentY);
  Task<ImmutableList<DisplayInfo>> GetDisplays();
  Task<DisplayInfo?> GetPrimaryDisplay();
  Task<Rectangle> GetVirtualScreenBounds();
  Task ReloadDisplays();
  Task<Result<DisplayInfo>> TryFindDisplay(string deviceName);
}