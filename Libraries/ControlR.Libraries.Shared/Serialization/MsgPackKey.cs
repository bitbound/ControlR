using System.Runtime.CompilerServices;

namespace ControlR.Libraries.Shared.Serialization;
public class MsgPackKey : KeyAttribute
{
    public MsgPackKey(int x)
        : base(x)
    {
    }

    public MsgPackKey([CallerMemberName] string? key = null)
        : base(ToCamelCase(key ?? string.Empty))
    {
    }

    private static string ToCamelCase(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return string.Empty;
        }

        if (key.Length == 1)
        {
            return key.ToLower();
        }

        return string.Join("", char.ToLower(key[0]), key[1..]);
    }
}
