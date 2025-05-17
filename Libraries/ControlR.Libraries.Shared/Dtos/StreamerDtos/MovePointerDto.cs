﻿using System.Text.Json.Serialization;

namespace ControlR.Libraries.Shared.Dtos.StreamerDtos;

[MessagePackObject(keyAsPropertyName: true)]
public record MovePointerDto(
    double PercentX,
    double PercentY);

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MovePointerType
{
  Absolute,
  Relative
}