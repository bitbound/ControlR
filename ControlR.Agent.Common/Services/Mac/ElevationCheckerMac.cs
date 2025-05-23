﻿using ControlR.Agent.Common.Interfaces;
using ControlR.Libraries.DevicesNative.Linux;

namespace ControlR.Agent.Common.Services.Mac;

public class ElevationCheckerMac : IElevationChecker
{
  public static IElevationChecker Instance { get; } = new ElevationCheckerMac();

  public bool IsElevated()
  {
    return Libc.Geteuid() == 0;
  }
}