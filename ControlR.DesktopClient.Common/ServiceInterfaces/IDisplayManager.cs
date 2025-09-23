using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using ControlR.DesktopClient.Common.Models;
using ControlR.Libraries.Shared.Dtos.HubDtos;

namespace ControlR.DesktopClient.Common.ServiceInterfaces;

public interface IDisplayManager
{
  Task<Point> ConvertPercentageLocationToAbsolute(string displayName, double percentX, double percentY);
  Task<ImmutableList<DisplayDto>> GetDisplays();
  DisplayInfo? GetPrimaryDisplay();
  Rectangle GetVirtualScreenBounds();
  Task ReloadDisplays();
  bool TryFindDisplay(string deviceName, [NotNullWhen(true)] out DisplayInfo? display);
}