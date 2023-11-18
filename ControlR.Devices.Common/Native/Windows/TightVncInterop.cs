using ControlR.Shared.Primitives;
using System.Runtime.InteropServices;

namespace ControlR.Devices.Common.Native.Windows;

public static partial class TightVncInterop
{
    public static Result<byte[]> EncryptVncPassword(string password)
    {
        try
        {
            var result = EncryptVncPassword(password, out var size);
            var buffer = new byte[size];
            Marshal.Copy(result, buffer, 0, size);

            Marshal.FreeCoTaskMem(result);

            return Result.Ok(buffer);
        }
        catch (Exception ex)
        {
            return Result.Fail<byte[]>(ex);
        }
    }

    [LibraryImport("ControlR.WinVncPassword.dll", StringMarshalling = StringMarshalling.Utf8)]
    private static partial nint EncryptVncPassword(string password, out int size);
}