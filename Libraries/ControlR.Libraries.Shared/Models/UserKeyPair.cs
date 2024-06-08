namespace ControlR.Libraries.Shared.Models;

public class UserKeyPair(byte[] publicKey, byte[] privateKey)
{
    public byte[] PrivateKey { get; private set; } = privateKey;
    public byte[] PublicKey { get; private set; } = publicKey;
}
