using System.Security.Cryptography;

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

  public static string GenerateString(int byteLength = 48)
  {
      var bytes = RandomNumberGenerator.GetBytes(byteLength);
      var base64 = Convert.ToBase64String(bytes);
      return base64.Replace('+', '-')
                  .Replace('/', '_')
                  .TrimEnd('=');
  }
}