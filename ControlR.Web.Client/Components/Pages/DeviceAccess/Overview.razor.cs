using ControlR.Web.Client.Services.DeviceAccess;
using Microsoft.AspNetCore.Components;

namespace ControlR.Web.Client.Components.Pages.DeviceAccess;

public partial class Overview
{
  [Inject]
  public required IDeviceAccessState DeviceAccessState { get; init; }
}
