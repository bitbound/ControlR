using Microsoft.AspNetCore.Components;

namespace ControlR.Viewer.Models;

public class DeviceContentInstance(DeviceDto device, RenderFragment content, string contentTypeName)
{
    public RenderFragment Content { get; } = content;
    public string ContentTypeName { get; } = contentTypeName;
    public DeviceDto Device { get; } = device;
    public Guid WindowId { get; } = Guid.NewGuid();
}