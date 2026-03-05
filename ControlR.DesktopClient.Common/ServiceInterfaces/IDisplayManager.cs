using System.Collections.Immutable;
using ControlR.DesktopClient.Common.Models;
using ControlR.Libraries.Shared.Primitives;

namespace ControlR.DesktopClient.Common.ServiceInterfaces;

public interface IDisplayManager
{
  bool IsPrivacyScreenEnabled
  {
    get => false;
  }
  Task<LogicalPoint> ConvertDisplayPercentToLogical(string displayName, double percentOfDisplayX, double percentOfDisplayY);
  Task<PhysicalPoint> ConvertDisplayPercentToPhysical(string displayName, double percentOfDisplayX, double percentOfDisplayY);
  Task<ImmutableList<DisplayInfo>> GetDisplays();
  Task<DisplayInfo?> GetPrimaryDisplay();
  Task<LogicalRect> GetVirtualScreenLogicalBounds();
  Task ReloadDisplays();
  Task<Result> SetPrivacyScreen(bool isEnabled);
  Task<Result<DisplayInfo>> TryFindDisplay(string deviceName);
}