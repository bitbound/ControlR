using System.Security.Cryptography;

namespace ControlR.Libraries.Shared.Helpers;

public class RandomGenerator
{
    private const string AllowableCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIGKLMNOPQRSTUVWXYZ0123456789";

    public static string CreateDeviceToken()
    {
        return GenerateString(128);
    }

    public static string GenerateString(int length)
    {
        var bytes = RandomNumberGenerator.GetBytes(length);
        return new string(bytes.Select(x => AllowableCharacters[x % AllowableCharacters.Length]).ToArray());
    }
}