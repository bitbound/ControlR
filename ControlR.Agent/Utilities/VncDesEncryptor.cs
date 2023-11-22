using System.Security.Cryptography;
using System.Text;

namespace ControlR.Agent.Utilities;

// From https://stackoverflow.com/a/39914451/5705257
public static class VncDesEncryptor
{
    public static string Decrypt(string password)
    {
        if (password.Length < 16)
        {
            return string.Empty;
        }

        byte[] key = [23, 82, 107, 6, 35, 78, 88, 7];
        byte[] passArr = Convert.FromHexString(password);
        byte[] response = new byte[passArr.Length];

        // reverse the byte order
        byte[] newkey = new byte[8];
        for (int i = 0; i < 8; i++)
        {
            // revert key[i]:
            newkey[i] = (byte)(
                (key[i] & 0x01) << 7 |
                (key[i] & 0x02) << 5 |
                (key[i] & 0x04) << 3 |
                (key[i] & 0x08) << 1 |
                (key[i] & 0x10) >> 1 |
                (key[i] & 0x20) >> 3 |
                (key[i] & 0x40) >> 5 |
                (key[i] & 0x80) >> 7
                );
        }
        key = newkey;
        // reverse the byte order

        var des = DES.Create();
        des.Padding = PaddingMode.None;
        des.Mode = CipherMode.ECB;

        ICryptoTransform dec = des.CreateDecryptor(key, null);
        dec.TransformBlock(passArr, 0, passArr.Length, response, 0);

        return Encoding.ASCII.GetString(response);
    }

    public static string Encrypt(string password)
    {
        if (password.Length > 8)
        {
            password = password[..8];
        }
        if (password.Length < 8)
        {
            password = password.PadRight(8, '\0');
        }

        byte[] key = [23, 82, 107, 6, 35, 78, 88, 7];
        byte[] passArr = Encoding.ASCII.GetBytes(password);
        byte[] response = new byte[passArr.Length];
        char[] chars = ['0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'A', 'B', 'C', 'D', 'E', 'F'];

        // reverse the byte order
        byte[] newkey = new byte[8];
        for (int i = 0; i < 8; i++)
        {
            // revert desKey[i]:
            newkey[i] = (byte)(
                (key[i] & 0x01) << 7 |
                (key[i] & 0x02) << 5 |
                (key[i] & 0x04) << 3 |
                (key[i] & 0x08) << 1 |
                (key[i] & 0x10) >> 1 |
                (key[i] & 0x20) >> 3 |
                (key[i] & 0x40) >> 5 |
                (key[i] & 0x80) >> 7
                );
        }
        key = newkey;
        // reverse the byte order

        var des = DES.Create();
        des.Padding = PaddingMode.None;
        des.Mode = CipherMode.ECB;

        ICryptoTransform enc = des.CreateEncryptor(key, null);
        enc.TransformBlock(passArr, 0, passArr.Length, response, 0);

        string hexString = string.Empty;
        for (int i = 0; i < response.Length; i++)
        {
            hexString += chars[response[i] >> 4];
            hexString += chars[response[i] & 0xf];
        }
        return hexString.Trim().ToLower();
    }

    private static byte[] ToByteArray(string HexString)
    {
        int NumberChars = HexString.Length;
        byte[] bytes = new byte[NumberChars / 2];

        for (int i = 0; i < NumberChars; i += 2)
        {
            bytes[i / 2] = Convert.ToByte(HexString.Substring(i, 2), 16);
        }

        return bytes;
    }
}