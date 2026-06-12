namespace ControlR.Libraries.Shared.Services.Encryption;

public record Ed25519KeyPair(byte[] PublicKey, byte[] PrivateKey);