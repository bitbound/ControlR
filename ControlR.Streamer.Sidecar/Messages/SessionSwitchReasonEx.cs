﻿namespace ControlR.Streamer.Sidecar.Messages;

public enum SessionSwitchReasonEx
{
    ConsoleConnect = 1,
    ConsoleDisconnect = 2,
    RemoteConnect = 3,
    RemoteDisconnect = 4,
    SessionLogon = 5,
    SessionLogoff = 6,
    SessionLock = 7,
    SessionUnlock = 8,
    SessionRemoteControl = 9
}
