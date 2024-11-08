﻿using System.Text.Json.Serialization;

namespace ControlR.Libraries.Shared.Dtos.StreamerDtos;

[MessagePackObject]
public record MovePointerDto(
    [property: MsgPackKey] double PercentX,
    [property: MsgPackKey] double PercentY);

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MovePointerType
{
  Absolute,
  Relative
}