using MessagePack;
using System.Runtime.Serialization;

namespace ControlR.Libraries.Ipc;

[DataContract]
public class MessageWrapper
{
  [SerializationConstructor]
  public MessageWrapper(string contentTypeName, byte[] content, MessageType messageType, Guid responseTo)
  {
    Id = Guid.NewGuid();
    ContentTypeName = contentTypeName;
    Content = content;
    MessageType = messageType;
    ResponseTo = responseTo;
  }

  public MessageWrapper(Type contentType, object content, MessageType messageType)
  {
    Id = Guid.NewGuid();
    Content = MessagePackSerializer.Serialize(contentType, content);
    ContentTypeName = contentType.FullName ?? contentType.Name;
    MessageType = messageType;
  }

  public MessageWrapper(Type contentType, object content, Guid responseTo)
      : this(contentType, content, MessageType.Response)
  {
    ResponseTo = responseTo;
  }

  [DataMember]
  public byte[] Content { get; set; }

  [DataMember]
  public string ContentTypeName { get; set; }

  [DataMember]
  public Guid Id { get; set; }

  [DataMember]
  public MessageType MessageType { get; set; }

  [DataMember]
  public Guid ResponseTo { get; set; }
}