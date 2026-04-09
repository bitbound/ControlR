using System.Runtime.Serialization;

namespace ControlR.Libraries.Api.Contracts.Enums;

public enum UserPreferenceThemeMode
{
  [EnumMember]
  Auto = 0,
  [EnumMember]
  Light = 1,
  [EnumMember]
  Dark = 2
}