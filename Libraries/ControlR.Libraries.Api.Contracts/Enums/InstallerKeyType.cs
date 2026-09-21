using System.Text.Json.Serialization;

namespace ControlR.Libraries.Api.Contracts.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum InstallerKeyType
{
  Unknown = 0,
  UsageBased = 1,
  TimeBased = 2,
  Persistent = 3
}
