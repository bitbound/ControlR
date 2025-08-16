using System.Runtime.Serialization;

namespace ControlR.Libraries.Ipc;

[DataContract]
public enum MessageType
{
  [EnumMember]
  Unspecified,

  [EnumMember]
  Send,

  [EnumMember]
  Invoke,

  [EnumMember]
  Response
}