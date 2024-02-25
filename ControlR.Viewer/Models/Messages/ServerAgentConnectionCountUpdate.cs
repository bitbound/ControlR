using ControlR.Shared.Dtos;
using ControlR.Shared.Serialization;
using MessagePack;

namespace ControlR.Viewer.Models.Messages;

[MessagePackObject]
public record ServerStatsUpdateMessage([property: MsgPackKey] ServerStatsDto ServerStats);