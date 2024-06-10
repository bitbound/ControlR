using System.Text.Json.Serialization;

namespace ControlR.Libraries.Shared.Dtos.SidecarDtos;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SidecarDtoType
{
    Unknown,
    DesktopChanged,
    DesktopRequest,
    MovePointer,
    MouseButtonEvent,
    KeyEvent,
    TypeText,
    ResetKeyboardState,
    WheelScroll
}
