using ControlR.Shared.Dtos;
using ControlR.Shared.Models;
using MessagePack;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;

namespace ControlR.Shared.Services;

public interface IKeyProvider
{
    SignedPayloadDto CreateRandomSignedDto(DtoType dtoType, byte[] privateKey);

    SignedPayloadDto CreateSignedDto<T>(T payload, DtoType dtoType, byte[] privateKey);

    KeypairExport ExportKeypair(string username, string password, byte[] privateKey);

    UserKeyPair GenerateKeys(string password);

    bool Verify(SignedPayloadDto signedDto);
}

public class KeyProvider(ISystemTime systemTime, ILogger<KeyProvider> logger) : IKeyProvider
{
    private readonly HashAlgorithmName _hashAlgName = HashAlgorithmName.SHA512;
    private readonly ILogger<KeyProvider> _logger = logger;
    private readonly PbeParameters _pbeParameters = new(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA512, 5_000);
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

    public KeypairExport ExportKeypair(string username, string password, byte[] privateKey)
    {
        using var rsa = RSA.Create();

        rsa.ImportRSAPrivateKey(privateKey, out _);
        var encryptedPrivateKey = rsa.ExportEncryptedPkcs8PrivateKey(password, _pbeParameters);
        var publicKey = rsa.ExportRSAPublicKey();

        var export = new KeypairExport()
        {
            EncryptedPrivateKey = Convert.ToBase64String(encryptedPrivateKey),
            PublicKey = Convert.ToBase64String(publicKey),
            Username = username
        };
        return export;
    }

    public UserKeyPair GenerateKeys(string password)
    {
        using var rsa = RSA.Create();
        var encryptedPrivateKey = rsa.ExportEncryptedPkcs8PrivateKey(password, _pbeParameters);
        var privateKey = rsa.ExportRSAPrivateKey();
        var publicKey = rsa.ExportRSAPublicKey();
        return new UserKeyPair(publicKey, privateKey, encryptedPrivateKey);
    }

    public bool Verify(SignedPayloadDto signedDto)
    {
        using var rsa = RSA.Create();
        rsa.ImportRSAPublicKey(signedDto.PublicKey, out _);

        if (!Verify(rsa, signedDto.Payload, signedDto.Signature))
        {
            return false;
        }

        if (!Verify(rsa, signedDto.Timestamp, signedDto.TimestampSignature))
        {
            return false;
        }

        if (signedDto.DtoType == DtoType.Identity)
        {
            return true;
        }

        var timestamp = MessagePackSerializer.Deserialize<DateTimeOffset>(signedDto.Timestamp);
        // Timestamp shouldn't be any older than 5 seconds.
        return timestamp > _systemTime.Now.AddSeconds(-5);
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
        var result = rsa.VerifyData(payload, signature, _hashAlgName, _signaturePadding);
        if (!result)
        {
            _logger.LogCritical("Verification failed for signature: {Signature}", Convert.ToBase64String(signature));
        }
        return result;
    }
}