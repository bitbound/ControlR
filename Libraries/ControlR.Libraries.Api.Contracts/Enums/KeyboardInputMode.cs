using System.Text.Json.Serialization;

namespace ControlR.Libraries.Api.Contracts.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum KeyboardInputMode
{
  Auto,
  Physical,
  Virtual,
}
