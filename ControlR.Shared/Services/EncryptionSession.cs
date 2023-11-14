using ControlR.Shared.Dtos;
using ControlR.Shared.Models;
using MessagePack;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;

namespace ControlR.Shared.Services;

public interface IEncryptionSession : IDisposable
{
    UserKeyPair? CurrentState { get; }

    SignedPayloadDto CreateRandomSignedDto(DtoType dtoType);

    SignedPayloadDto CreateSignedDto<T>(T payload, DtoType dtoType);

    Result<KeypairExport> ExportKeypair(string username);

    UserKeyPair GenerateKeys(string password);

    Result<UserKeyPair> ImportPrivateKey(string password, byte[] encryptedKey);

    void ImportPublicKey(byte[] publicKey);

    void ImportPublicKey(string publicKeyBase64);

    void Reset();

    UserKeyPair RestoreState();

    void SaveState();

    byte[] Sign(byte[] payload);

    bool Verify(string payloadBase64, string signatureBase64);

    bool Verify(byte[] payload, byte[] signature);

    bool Verify(SignedPayloadDto signedDto);
}

public class EncryptionSession(ISystemTime systemTime, ILogger<EncryptionSession> logger) : IEncryptionSession
{
    private readonly ILogger<EncryptionSession> _logger = logger;
    private readonly PbeParameters _pbeParameters = new(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA512, 5_000);
    private readonly ISystemTime _systemTime = systemTime;
    private UserKeyPair? _backupKeys;
    private RSAParameters? _backupParams;
    private UserKeyPair? _currentKeys;
    private RSA _rsa = RSA.Create();

    public UserKeyPair? CurrentState => _currentKeys;

    public SignedPayloadDto CreateRandomSignedDto(DtoType dtoType)
    {
        var payload = RandomNumberGenerator.GetBytes(256);
        return CreateSignedDtoImpl(payload, dtoType);
    }

    public SignedPayloadDto CreateSignedDto<T>(T payload, DtoType dtoType)
    {
        var payloadBytes = MessagePackSerializer.Serialize(payload);
        return CreateSignedDtoImpl(payloadBytes, dtoType);
    }

    public void Dispose()
    {
        _rsa.Dispose();
        GC.SuppressFinalize(this);
    }

    public Result<KeypairExport> ExportKeypair(string username)
    {
        if (_currentKeys is null)
        {
            return Result.Fail<KeypairExport>("There are no keys to export.");
        }

        var export = new KeypairExport()
        {
            EncryptedPrivateKey = Convert.ToBase64String(_currentKeys.EncryptedPrivateKey),
            PublicKey = Convert.ToBase64String(_currentKeys.PublicKey),
            Username = username
        };
        return Result.Ok(export);
    }

    public UserKeyPair GenerateKeys(string password)
    {
        _rsa.Dispose();
        _rsa = RSA.Create();
        var encryptedPrivateKey = _rsa.ExportEncryptedPkcs8PrivateKey(password, _pbeParameters);
        var privateKey = _rsa.ExportRSAPrivateKey();
        var publicKey = _rsa.ExportRSAPublicKey();
        _currentKeys = new UserKeyPair(publicKey, privateKey, encryptedPrivateKey);
        return _currentKeys;
    }

    public Result<UserKeyPair> ImportPrivateKey(string password, byte[] encryptedKey)
    {
        try
        {
            _rsa.ImportEncryptedPkcs8PrivateKey(password, encryptedKey, out var bytesRead);
            var privateKey = _rsa.ExportRSAPrivateKey();
            var publicKey = _rsa.ExportRSAPublicKey();
            var encryptedPrivateKey = encryptedKey;
            _currentKeys = new UserKeyPair(publicKey, privateKey, encryptedPrivateKey);
            return Result.Ok(_currentKeys);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while importing private key.");
            return Result.Fail<UserKeyPair>(ex);
        }
    }

    public void ImportPublicKey(byte[] publicKeyBytes)
    {
        _rsa.ImportRSAPublicKey(publicKeyBytes, out _);
    }

    public void ImportPublicKey(string publicKey)
    {
        ImportPublicKey(Convert.FromBase64String(publicKey));
    }

    public void Reset()
    {
        _rsa.Dispose();
        _rsa = RSA.Create();
        _currentKeys = null;
    }

    public UserKeyPair RestoreState()
    {
        if (_backupKeys is null || !_backupParams.HasValue)
        {
            throw new Exception("No backup state to restore.");
        }

        _rsa.ImportParameters(_backupParams.Value);
        _currentKeys = _backupKeys;

        _backupKeys = null;
        _backupParams = null;

        return _currentKeys;
    }

    public void SaveState()
    {
        if (_currentKeys is null)
        {
            throw new Exception("No current keys to save.");
        }

        _backupParams = _rsa.ExportParameters(true);
        _backupKeys = (UserKeyPair)_currentKeys.Clone();
    }

    public byte[] Sign(byte[] payload)
    {
        return _rsa.SignData(payload, HashAlgorithmName.SHA512, RSASignaturePadding.Pkcs1);
    }

    public bool Verify(byte[] payload, byte[] signature)
    {
        var result = _rsa.VerifyData(payload, signature, HashAlgorithmName.SHA512, RSASignaturePadding.Pkcs1);
        if (!result)
        {
            _logger.LogCritical("Verification failed for signature: {Signature}", Convert.ToBase64String(signature));
        }
        return result;
    }

    public bool Verify(string payloadBase64, string signatureBase64)
    {
        return Verify(Convert.FromBase64String(payloadBase64), Convert.FromBase64String(signatureBase64));
    }

    public bool Verify(SignedPayloadDto signedDto)
    {
        ImportPublicKey(signedDto.PublicKey);
        if (!Verify(signedDto.Payload, signedDto.Signature))
        {
            return false;
        }

        // TODO: Remove after devices have updated.
        if (signedDto.Timestamp is null)
        {
            return true;
        }

        if (signedDto.TimestampSignature is null)
        {
            return false;
        }

        if (!Verify(signedDto.Timestamp, signedDto.TimestampSignature))
        {
            return false;
        }

        var timestamp = MessagePackSerializer.Deserialize<DateTimeOffset>(signedDto.Timestamp);
        // Timestamp shouldn't be any older than 5 seconds.
        return timestamp > _systemTime.Now.AddSeconds(-5);
    }

    private SignedPayloadDto CreateSignedDtoImpl(byte[] payload, DtoType dtoType)
    {
        if (CurrentState is null)
        {
            throw new InvalidOperationException("A keypair must be generated before DTOs can be signed.");
        }

        var timestamp = MessagePackSerializer.Serialize(_systemTime.Now);
        var timestampSignature = Sign(timestamp);

        var signature = Sign(payload);
        return new SignedPayloadDto()
        {
            DtoType = dtoType,
            Payload = payload,
            Signature = signature,
            PublicKey = CurrentState.PublicKey,
            PublicKeyPem = _rsa.ExportSubjectPublicKeyInfoPem(),
            Timestamp = timestamp,
            TimestampSignature = timestampSignature,
        };
    }
}