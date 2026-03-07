using System.Collections.Immutable;
using System.Drawing;
using ControlR.DesktopClient.Common.Models;
using ControlR.Libraries.Shared.Primitives;

namespace ControlR.DesktopClient.Common.ServiceInterfaces;

public interface IDisplayManager
{
  bool IsPrivacyScreenEnabled
  {
    get => false;
  }
  Task<ImmutableList<DisplayInfo>> GetDisplays();
  Task<DisplayInfo?> GetPrimaryDisplay();
  Task<Rectangle> GetVirtualScreenLayoutBounds();
  Task ReloadDisplays();
  Task<Result> SetPrivacyScreen(bool isEnabled);
  Task<Result<DisplayInfo>> TryFindDisplay(string deviceName);
}