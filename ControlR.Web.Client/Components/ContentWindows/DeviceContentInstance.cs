using Microsoft.AspNetCore.Components;

namespace ControlR.Web.Client.Components.ContentWindows;

public class DeviceContentInstance(DeviceViewModel device, RenderFragment content, DeviceContentInstanceType contentType)
{
  public RenderFragment Content { get; } = content;
  public DeviceContentInstanceType ContentType { get; } = contentType;

  public string ContentTypeName
  {
    get
    {
      return ContentType switch
      {
        DeviceContentInstanceType.RemoteControl => "Remote",
        DeviceContentInstanceType.Terminal => "Terminal",
        DeviceContentInstanceType.VncFrame => "VNC",
        _ => "Content"
      };
    }
  }

  public DeviceViewModel DeviceUpdate { get; } = device;
  public Guid WindowId { get; } = Guid.NewGuid();
}

public enum DeviceContentInstanceType
{
  RemoteControl,
  Terminal,
  VncFrame
}