using ControlR.Libraries.Shared.Dtos;
using ControlR.Libraries.Shared.Models;
using MessagePack;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;

namespace ControlR.Libraries.Shared.Services;

public interface IKeyProvider
{
    SignedPayloadDto CreateRandomSignedDto(DtoType dtoType, byte[] privateKey);
    SignedPayloadDto CreateSignedDto<T>(T payload, DtoType dtoType, byte[] privateKey);
    byte[] EncryptPrivateKey(string password, byte[] privateKey);
    UserKeyPair GenerateKeys();
    UserKeyPair ImportPrivateKey(string password, byte[] encryptedPrivateKey);
    UserKeyPair ImportPrivateKey(byte[] privateKey);
    bool Verify(SignedPayloadDto signedDto);
}

public class KeyProvider(ISystemTime systemTime, ILogger<KeyProvider> logger) : IKeyProvider
{
    private readonly HashAlgorithmName _hashAlgName = HashAlgorithmName.SHA512;
    private readonly ILogger<KeyProvider> _logger = logger;
    private readonly PbeParameters _pbeParameters = new(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA512, 10_000);
    private readonly RSASignaturePadding _signaturePadding = RSASignaturePadding.Pkcs1;
    private readonly ISystemTime _systemTime = systemTime;

    public SignedPayloadDto CreateRandomSignedDto(DtoType dtoType, byte[] privateKey)
    {
        using var rsa = RSA.Create();
        rsa.ImportRSAPrivateKey(privateKey, out _);
        var payload = RandomNumberGenerator.GetBytes(256);
        return CreateSignedDtoImpl(rsa, payload, dtoType);
    }

    public SignedPayloadDto CreateSignedDto<T>(T payload, DtoType dtoType, byte[] privateKey)
    {
        using var rsa = RSA.Create();
        rsa.ImportRSAPrivateKey(privateKey, out _);
        var payloadBytes = MessagePackSerializer.Serialize(payload);
        return CreateSignedDtoImpl(rsa, payloadBytes, dtoType);
    }
    public byte[] EncryptPrivateKey(string password, byte[] privateKey)
    {
        using var rsa = RSA.Create();

        rsa.ImportRSAPrivateKey(privateKey, out _);
        return rsa.ExportEncryptedPkcs8PrivateKey(password, _pbeParameters);
    }

    public UserKeyPair GenerateKeys()
    {
        using var rsa = RSA.Create();
        var privateKey = rsa.ExportRSAPrivateKey();
        var publicKey = rsa.ExportRSAPublicKey();
        return new UserKeyPair(publicKey, privateKey);
    }

    public UserKeyPair ImportPrivateKey(string password, byte[] encryptedPrivateKey)
    {
        using var rsa = RSA.Create();
        rsa.ImportEncryptedPkcs8PrivateKey(password, encryptedPrivateKey, out _);
        var privateKey = rsa.ExportRSAPrivateKey();
        var publicKey = rsa.ExportRSAPublicKey();
        return new UserKeyPair(publicKey, privateKey);
    }

    public UserKeyPair ImportPrivateKey(byte[] privateKey)
    {
        using var rsa = RSA.Create();
        rsa.ImportRSAPrivateKey(privateKey, out _);
        var publicKey = rsa.ExportRSAPublicKey();
        return new UserKeyPair(publicKey, privateKey);
    }

    public bool Verify(SignedPayloadDto signedDto)
    {
        using var rsa = RSA.Create();
        rsa.ImportRSAPublicKey(signedDto.PublicKey, out _);

        if (!Verify(rsa, signedDto.Payload, signedDto.Signature))
        {
            _logger.LogCritical("Key verification failed. DTO type: {DtoType}.  " +
                "Public key: {PublicKey}.  " +
                "Signature: {Signature}",
                signedDto.DtoType,
                signedDto.PublicKeyBase64,
                Convert.ToBase64String(signedDto.Signature));
            return false;
        }

        if (!Verify(rsa, signedDto.Timestamp, signedDto.TimestampSignature))
        {
            _logger.LogCritical("Timestamp verification failed. " +
                "DTO type: {DtoType}.  " +
                "Public key: {PublicKey}.  " +
                "Timestamp Signature: {Signature}",
                signedDto.DtoType,
                signedDto.PublicKeyBase64,
                Convert.ToBase64String(signedDto.TimestampSignature));
            return false;
        }

        if (signedDto.DtoType == DtoType.IdentityAttestation)
        {
            return true;
        }

        var timestamp = MessagePackSerializer.Deserialize<DateTimeOffset>(signedDto.Timestamp);
        // Timestamp shouldn't be any older than 10 seconds.
        var result = timestamp > _systemTime.Now.AddSeconds(-10);
        if (!result)
        {
            _logger.LogCritical("Timestamp has expired. Are clocks set correctly on both ends? " +
              "DTO type: {DtoType}.  " +
              "Public key: {PublicKey}.  ",
              signedDto.DtoType,
              signedDto.PublicKeyBase64);
        }

        return result;
    }

    private SignedPayloadDto CreateSignedDtoImpl(RSA rsa, byte[] payload, DtoType dtoType)
    {
        var timestamp = MessagePackSerializer.Serialize(_systemTime.Now);
        var timestampSignature = Sign(rsa, timestamp);

        var signature = Sign(rsa, payload);
        return new SignedPayloadDto()
        {
            DtoType = dtoType,
            Payload = payload,
            Signature = signature,
            PublicKey = rsa.ExportRSAPublicKey(),
            PublicKeyPem = rsa.ExportSubjectPublicKeyInfoPem(),
            Timestamp = timestamp,
            TimestampSignature = timestampSignature,
        };
    }

    private byte[] Sign(RSA rsa, byte[] payload)
    {
        return rsa.SignData(payload, _hashAlgName, _signaturePadding);
    }

    private bool Verify(RSA rsa, byte[] payload, byte[] signature)
    {
        return rsa.VerifyData(payload, signature, _hashAlgName, _signaturePadding);
    }
}