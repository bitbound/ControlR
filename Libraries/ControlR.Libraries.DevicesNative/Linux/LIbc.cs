﻿using System.Runtime.InteropServices;

namespace ControlR.Devices.Native.Linux;

public partial class Libc
{
  [LibraryImport("libc", SetLastError = true)]
  public static partial uint Geteuid();

  [LibraryImport("libc", SetLastError = true)]
  public static partial int Setsid();
}