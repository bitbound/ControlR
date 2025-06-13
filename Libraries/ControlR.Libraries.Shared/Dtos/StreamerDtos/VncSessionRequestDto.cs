﻿using System.Text.Json.Serialization;

namespace ControlR.Libraries.Shared.Dtos.StreamerDtos;

[MessagePackObject(keyAsPropertyName: true)]
[method: JsonConstructor]
[method: SerializationConstructor]
public record VncSessionRequestDto(
  Guid SessionId,
  Uri WebsocketUri,
  string ViewerConnectionId,
  Guid DeviceId,
  bool NotifyUserOnSessionStart,
  string ViewerName = "");