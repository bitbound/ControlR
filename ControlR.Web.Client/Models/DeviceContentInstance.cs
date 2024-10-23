using Microsoft.AspNetCore.Components;

namespace ControlR.Web.Client.Models;

public class DeviceContentInstance(DeviceResponseDto device, RenderFragment content, DeviceContentInstanceType contentType)
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
        _ => "Content"
      };
    }
  }

  public DeviceResponseDto Device { get; } = device;
  public Guid WindowId { get; } = Guid.NewGuid();
}

public enum DeviceContentInstanceType
{
  RemoteControl,
  Terminal
}