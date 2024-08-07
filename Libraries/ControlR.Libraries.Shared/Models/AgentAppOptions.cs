﻿using ControlR.Libraries.Shared.Collections;
using ControlR.Libraries.Shared.Dtos;

namespace ControlR.Libraries.Shared.Models;

[MessagePackObject]
public class AgentAppOptions
{
    public const string SectionKey = "AppOptions";

    [MsgPackKey]
    public ConcurrentList<AuthorizedKeyDto> AuthorizedKeys { get; set; } = [];


    [MsgPackKey]
    public string DeviceId { get; set; } = string.Empty;

    [MsgPackKey]
    public Uri? ServerUri { get; set; }
}