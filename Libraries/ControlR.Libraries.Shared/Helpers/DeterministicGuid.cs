using System.Security.Cryptography;

namespace ControlR.Libraries.Shared.Helpers;
public static class DeterministicGuid
{
  public static Guid Create(int seed)
  {
    var seedBytes = BitConverter.GetBytes(seed);
    var hash = MD5.HashData(seedBytes);
    return new Guid(hash);
  }
}
