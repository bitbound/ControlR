using System.Text.Json.Serialization;

namespace ControlR.Libraries.Shared.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RuntimeId
{
  [JsonStringEnumMemberName("win-x86")]
  WinX86,

  [JsonStringEnumMemberName("win-x64")]
  WinX64,

  [JsonStringEnumMemberName("osx-arm64")]
  MacOsArm64,

  [JsonStringEnumMemberName("osx-x64")]
  MacOsX64,

  [JsonStringEnumMemberName("linux-x64")]
  LinuxX64
}