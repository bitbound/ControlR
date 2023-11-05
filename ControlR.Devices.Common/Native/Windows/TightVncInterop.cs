using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using System.Text;
using System.Threading.Tasks;

namespace ControlR.Devices.Common.Native.Windows;
public static partial class TightVncInterop
{
    public static byte[] EncryptVncPassword(string password)
    {
        var result = EncryptVncPassword(password, out var size);
        var buffer = new byte[size];
        Marshal.Copy(result, buffer, 0, size);

        Marshal.FreeCoTaskMem(result);

        return buffer;

    }
    [LibraryImport("ControlR.WinVncPassword.dll", StringMarshalling = StringMarshalling.Utf8)]
    private static partial nint EncryptVncPassword(string password, out int size);
}
