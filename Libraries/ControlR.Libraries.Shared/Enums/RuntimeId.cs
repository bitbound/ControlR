using System.Text.Json.Serialization;

namespace ControlR.Libraries.Shared.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RuntimeId
{
  WinX86,
  WinX64,
  MacOsArm64,
  MacOsX64,
  LinuxX64
}