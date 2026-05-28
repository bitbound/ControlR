using System.Security.Cryptography;
using System.Text;

namespace ControlR.Libraries.Shared.Helpers;

public class RandomGenerator
{
  public static string CreateAccessToken()
  {
    return GenerateString(64);
  }

  public static string CreateApiKey()
  {
    return GenerateString(64);
  }

  public static string CreateDeviceToken()
  {
    return GenerateString(128);
  }

  public static string GeneratePassword(int length = 12, bool includeUppercase = true, bool includeLowercase = true, bool includeDigits = true, bool includeSpecialChars = true)
  {
    const string upper = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    const string lower = "abcdefghijklmnopqrstuvwxyz";
    const string digits = "0123456789";
    const string special = "!@#$%&*-=+.?";

    if (length < 1)
      throw new ArgumentOutOfRangeException(nameof(length), "Length must be at least 1.");

    var required = new List<char>();
    var pool = new StringBuilder();

    AddIf(upper, includeUppercase);
    AddIf(lower, includeLowercase);
    AddIf(digits, includeDigits);
    AddIf(special, includeSpecialChars);

    if (required.Count == 0)
      throw new ArgumentException("At least one character type must be included.");

    if (length < required.Count)
      throw new ArgumentException($"Length ({length}) must be at least the number of required character types ({required.Count}).");

    var allChars = pool.ToString();
    var buffer = new char[length];

    for (var i = 0; i < required.Count; i++)
      buffer[i] = required[i];

    for (var i = required.Count; i < length; i++)
      buffer[i] = allChars[RandomNumberGenerator.GetInt32(allChars.Length)];

    for (var i = length - 1; i > 0; i--)
    {
      var j = RandomNumberGenerator.GetInt32(i + 1);
      (buffer[i], buffer[j]) = (buffer[j], buffer[i]);
    }

    return new string(buffer);

    void AddIf(string chars, bool include)
    {
      if (include)
      {
        required.Add(chars[RandomNumberGenerator.GetInt32(chars.Length)]);
        pool.Append(chars);
      }
    }
  }

  public static string GenerateString(int byteLength = 48)
  {
      var bytes = RandomNumberGenerator.GetBytes(byteLength);
      var base64 = Convert.ToBase64String(bytes);
      return base64.Replace('+', '-')
                  .Replace('/', '_')
                  .TrimEnd('=');
  }
}