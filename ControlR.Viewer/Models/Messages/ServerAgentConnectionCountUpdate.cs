using ControlR.Shared.Serialization;
using MessagePack;

namespace ControlR.Viewer.Models.Messages;

[MessagePackObject]
public record ServerAgentConnectionCountUpdate([property: MsgPackKey] int AgentCount);