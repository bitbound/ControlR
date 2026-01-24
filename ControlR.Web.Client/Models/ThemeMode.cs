using System.Runtime.Serialization;

namespace ControlR.Web.Client.Models;

public enum ThemeMode
{
  [EnumMember]
  Auto = 0,
  [EnumMember]
  Light = 1,
  [EnumMember]
  Dark = 2
}
