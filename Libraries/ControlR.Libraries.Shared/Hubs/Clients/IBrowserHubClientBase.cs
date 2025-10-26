using ControlR.Libraries.Shared.Dtos.ServerApi;
using ControlR.Libraries.Shared.Models;

namespace ControlR.Libraries.Shared.Hubs.Clients;

public interface IBrowserHubClientBase : IHubClient
{
  Task InvokeToast(ToastInfo toastInfo);
  Task ReceiveDeviceUpdate(DeviceDto deviceDto);
}