using System.Security.Cryptography;

namespace ControlR.Libraries.Shared.Helpers;

public static class GuidHelper
{
  public static Guid CreateDeterministicGuid(int seed)
  {
    var bytes = BitConverter.GetBytes(seed);
    bytes = MD5.HashData(bytes);
    return new Guid(bytes);
  }
}
