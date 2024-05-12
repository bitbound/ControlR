using System.Text.Json.Serialization;

namespace ControlR.Shared.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum JsKeyType
{
    Unknown,
    Key,
    Code
}
