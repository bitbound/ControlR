using Microsoft.AspNetCore.Components;

namespace ControlR.Viewer.Models;

public class DeviceContentInstance(DeviceDto _device, RenderFragment _content, DeviceContentInstanceType _contentType)
{
    public RenderFragment Content { get; } = _content;
    public DeviceContentInstanceType ContentType { get; } = _contentType;
    public string ContentTypeName
    {
        get
        {
            return ContentType switch 
            { 
                DeviceContentInstanceType.RemoteControl => "Remote", 
                DeviceContentInstanceType.Terminal => "Terminal",
                _ => "Content" };
        }
    }
    public DeviceDto Device { get; } = _device;
    public Guid WindowId { get; } = Guid.NewGuid();
}

public enum DeviceContentInstanceType
{
    RemoteControl,
    Terminal
}