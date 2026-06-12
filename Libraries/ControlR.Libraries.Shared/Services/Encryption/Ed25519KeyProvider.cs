using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Security;

namespace ControlR.Libraries.Shared.Services.Encryption;

/// <summary>
/// Provides methods for generating Ed25519 key pairs, deriving public keys from private keys, signing DTOs, and verifying signatures. 
/// This is used for device identity bootstrapping and message authentication.
/// </summary>
public interface IEd25519KeyProvider
{
  /// <summary>
  /// Derives the Ed25519 public key from the given private key.
  /// </summary>
  /// <param name="privateKey">The 32-byte Ed25519 private key seed.</param>
  /// <returns>The 32-byte Ed25519 public key.</returns>
  byte[] DerivePublicKey(byte[] privateKey);
  /// <summary>
  /// Derives the Ed25519 public key from the given private key, with both keys represented as base64 strings.
  /// </summary> <param name="privateKeyBase64">The base64-encoded 32-byte Ed25519 private key seed.</param>
  /// <returns>The base64-encoded 32-byte Ed25519 public key.</returns>
  string DerivePublicKeyBase64(string privateKeyBase64);
  /// <summary>
  /// Generates a new Ed25519 key pair using a cryptographically secure random seed.
  /// </summary>
  /// <returns>A <see cref="Ed25519KeyPair"/> containing the 32-byte public and private keys.</returns>
  Ed25519KeyPair GenerateKeyPair();
  /// <summary>
  /// Signs a DTO by serializing it with MessagePack, then creating a <see cref="SignedDto{T}"/>
  /// envelope containing the signature, timestamp, and optional public key.
  /// </summary>
  /// <typeparam name="T">The type of DTO to sign.</typeparam>
  /// <param name="dto">The DTO to sign.</param>
  /// <param name="privateKey">The 32-byte Ed25519 private key.</param>
  /// <param name="publicKey">
  /// Optional base64-encoded public key to include in the envelope.
  /// Required for identity bootstrapping when the server has no stored public key for the caller.
  /// </param>
  /// <returns>A <see cref="SignedDto{T}"/> containing the DTO, timestamp, signature, and optional public key.</returns>
  SignedDto<T> Sign<T>(T dto, byte[] privateKey, string? publicKey = null);
  /// <summary>
  /// Validates that a base64-encoded string represents a valid Ed25519 public key (32 bytes).
  /// </summary>
  /// <param name="publicKeyBase64">
  /// The base64-encoded public key to validate.
  /// If null or whitespace, validation fails.
  /// </param>
  /// <returns>
  /// A <see cref="Result{T}"/> containing the 32-byte public key on success,
  /// or a failure with a reason string on invalid input.
  /// </returns>
  Result<byte[]> ValidatePublicKeyBase64(string? publicKeyBase64);
  /// <summary>
  /// Verifies a <see cref="SignedDto{T}"/> by re-serializing its inner DTO with MessagePack
  /// and checking the Ed25519 signature against the provided public key.
  /// </summary>
  /// <typeparam name="T">The type of DTO that was signed.</typeparam>
  /// <param name="signedDto">The signed envelope to verify.</param>
  /// <param name="publicKey">The 32-byte Ed25519 public key to verify against.</param>
  /// <returns><c>true</c> if the signature is valid; otherwise <c>false</c>.</returns>
  bool Verify<T>(SignedDto<T> signedDto, byte[] publicKey);
  /// <summary>
  /// Verifies that the <see cref="SignedDto{T}"/> timestamp is within the allowed clock skew.
  /// </summary>
  /// <typeparam name="T">The type of DTO that was signed.</typeparam>
  /// <param name="signedDto">The signed envelope to check.</param>
  /// <param name="clockSkew">The maximum allowed difference between the signed timestamp and the current UTC time.</param>
  /// <returns><c>true</c> if the timestamp is within the allowed clock skew; otherwise <c>false</c>.</returns>
  bool VerifyTimestamp<T>(SignedDto<T> signedDto, TimeSpan clockSkew);
}

public class Ed25519KeyProvider(TimeProvider timeProvider) : IEd25519KeyProvider
{
  private readonly TimeProvider _timeProvider = timeProvider;

  public byte[] DerivePublicKey(byte[] privateKey)
  {
    var privateKeyParams = new Ed25519PrivateKeyParameters(privateKey, 0);
    return privateKeyParams.GeneratePublicKey().GetEncoded();
  }

  public string DerivePublicKeyBase64(string privateKeyBase64)
  {
    var privateKey = Convert.FromBase64String(privateKeyBase64);
    var publicKey = DerivePublicKey(privateKey);
    return Convert.ToBase64String(publicKey);
  }

  public Ed25519KeyPair GenerateKeyPair()
  {
    var generator = new Ed25519KeyPairGenerator();
    generator.Init(new Ed25519KeyGenerationParameters(new SecureRandom()));
    var keyPair = generator.GenerateKeyPair();
    var publicKey = ((Ed25519PublicKeyParameters)keyPair.Public).GetEncoded();
    var privateKey = ((Ed25519PrivateKeyParameters)keyPair.Private).GetEncoded();
    return new Ed25519KeyPair(publicKey, privateKey);
  }

  public SignedDto<T> Sign<T>(T dto, byte[] privateKey, string? publicKey = null)
  {
    var dtoBytes = MessagePackSerializer.Serialize(dto);
    var timestamp = _timeProvider.GetUtcNow();
    var signedPayload = new SignedPayload(dtoBytes, timestamp.Ticks, publicKey);
    var payload = MessagePackSerializer.Serialize(signedPayload);

    var signer = new Ed25519Signer();
    signer.Init(true, new Ed25519PrivateKeyParameters(privateKey, 0));
    signer.BlockUpdate(payload, 0, payload.Length);
    var signature = signer.GenerateSignature();

    return new SignedDto<T>(dto, timestamp, signature, publicKey);
  }

  public Result<byte[]> ValidatePublicKeyBase64(string? publicKeyBase64)
  {
    if (string.IsNullOrWhiteSpace(publicKeyBase64))
    {
      return Result.Fail<byte[]>("Public key is required.");
    }

    byte[] publicKeyBytes;
    try
    {
      publicKeyBytes = Convert.FromBase64String(publicKeyBase64);
    }
    catch (FormatException)
    {
      return Result.Fail<byte[]>("Invalid public key format.");
    }

    if (publicKeyBytes.Length != 32)
    {
      return Result.Fail<byte[]>("Invalid public key length.");
    }

    return Result.Ok(publicKeyBytes);
  }

  public bool Verify<T>(SignedDto<T> signedDto, byte[] publicKey)
  {
    var dtoBytes = MessagePackSerializer.Serialize(signedDto.Dto);
    var signedPayload = new SignedPayload(dtoBytes, signedDto.Timestamp.Ticks, signedDto.PublicKey);
    var payload = MessagePackSerializer.Serialize(signedPayload);

    var signer = new Ed25519Signer();
    signer.Init(false, new Ed25519PublicKeyParameters(publicKey, 0));
    signer.BlockUpdate(payload, 0, payload.Length);
    return signer.VerifySignature(signedDto.Signature);
  }

  public bool VerifyTimestamp<T>(SignedDto<T> signedDto, TimeSpan clockSkew)
  {
    var now = _timeProvider.GetUtcNow();
    return Math.Abs((now - signedDto.Timestamp).Ticks) <= clockSkew.Ticks;
  }

  [MessagePackObject(AllowPrivate = true)]
  internal record SignedPayload(
    [property: Key(0)] byte[] DtoBytes,
    [property: Key(1)] long TimestampTicks,
    [property: Key(2)] string? PublicKeyBase64 = null);
}
