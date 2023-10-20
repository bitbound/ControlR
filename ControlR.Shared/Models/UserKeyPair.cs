namespace ControlR.Shared.Models;

public class UserKeyPair(byte[] publicKey, byte[] privateKey, byte[] encryptedPrivateKey) : ICloneable
{
    public byte[] EncryptedPrivateKey { get; private set; } = encryptedPrivateKey;
    public byte[] PrivateKey { get; private set; } = privateKey;
    public byte[] PublicKey { get; private set; } = publicKey;

    public object Clone()
    {
        return new UserKeyPair(PublicKey, PrivateKey, EncryptedPrivateKey);
    }
}
