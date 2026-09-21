using System.Text.Json.Serialization;

namespace ControlR.Libraries.Api.Contracts.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ThemeMode
{
  Auto = 0,
  Light = 1,
  Dark = 2
}
