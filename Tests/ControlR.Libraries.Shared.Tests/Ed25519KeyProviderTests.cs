using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Shared.Services.Encryption;
using MessagePack;
using Microsoft.Extensions.Time.Testing;

namespace ControlR.Libraries.Shared.Tests;

public class Ed25519KeyProviderTests
{
  private readonly Ed25519KeyProvider _provider;
  private readonly FakeTimeProvider _timeProvider;

  public Ed25519KeyProviderTests()
  {
    _timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
    _provider = new Ed25519KeyProvider(_timeProvider);
  }

  [Fact]
  public void DerivePublicKey_ReturnsCorrectPublicKey()
  {
    var keyPair = _provider.GenerateKeyPair();

    var derivedPublicKey = _provider.DerivePublicKey(keyPair.PrivateKey);

    Assert.Equal(keyPair.PublicKey, derivedPublicKey);
  }

  [Fact]
  public void GenerateKeyPair_ProducesCorrectSizedKeys()
  {
    var keyPair = _provider.GenerateKeyPair();

    Assert.Equal(32, keyPair.PrivateKey.Length);
    Assert.Equal(32, keyPair.PublicKey.Length);
  }

  [Fact]
  public void GenerateKeyPair_ProducesDifferentKeysEachTime()
  {
    var keyPair1 = _provider.GenerateKeyPair();
    var keyPair2 = _provider.GenerateKeyPair();

    Assert.NotEqual(keyPair1.PrivateKey, keyPair2.PrivateKey);
    Assert.NotEqual(keyPair1.PublicKey, keyPair2.PublicKey);
  }

  [Fact]
  public void SignAndVerify_MultipleRoundTrips_AllSucceed()
  {
    var keyPair = _provider.GenerateKeyPair();

    for (var i = 0; i < 10; i++)
    {
      var dto = new TestPayload($"message-{i}", i);
      var signedDto = _provider.Sign(dto, keyPair.PrivateKey);
      var isValid = _provider.Verify(signedDto, keyPair.PublicKey);
      Assert.True(isValid);
    }
  }

  [Fact]
  public void SignAndVerify_RoundTrip_Succeeds()
  {
    var keyPair = _provider.GenerateKeyPair();
    var dto = new TestPayload("test message", 123);

    var signedDto = _provider.Sign(dto, keyPair.PrivateKey, Convert.ToBase64String(keyPair.PublicKey));
    var signedBytes = MessagePackSerializer.Serialize(signedDto, cancellationToken: TestContext.Current.CancellationToken);

    var deserializedSignedDto = MessagePackSerializer.Deserialize<SignedDto<TestPayload>>(signedBytes, cancellationToken: TestContext.Current.CancellationToken);
    var publicKeyBase64 = Convert.FromBase64String(deserializedSignedDto.PublicKey!);

    var isValid = _provider.Verify(deserializedSignedDto, publicKeyBase64);
    Assert.True(isValid);
  }

  [Fact]
  public void Sign_IncludesTimestampWithinReasonableRange()
  {
    var keyPair = _provider.GenerateKeyPair();
    var dto = new TestPayload("test", 1);
    var before = _timeProvider.GetUtcNow();

    var signedDto = _provider.Sign(dto, keyPair.PrivateKey);
    var after = _timeProvider.GetUtcNow();

    Assert.True(signedDto.Timestamp >= before);
    Assert.True(signedDto.Timestamp <= after);
  }

  [Fact]
  public void Sign_PreservesPublicKeyInEnvelope()
  {
    var keyPair = _provider.GenerateKeyPair();
    var dto = new TestPayload("test", 1);
    var publicKeyBase64 = Convert.ToBase64String(keyPair.PublicKey);

    var signedDto = _provider.Sign(dto, keyPair.PrivateKey, publicKeyBase64);

    Assert.Equal(publicKeyBase64, signedDto.PublicKey);
  }

  [Fact]
  public void Sign_ProducesCorrectSizedSignature()
  {
    var keyPair = _provider.GenerateKeyPair();
    var dto = new TestPayload("hello", 42);

    var signedDto = _provider.Sign(dto, keyPair.PrivateKey);

    Assert.Equal(64, signedDto.Signature.Length);
  }

  [Fact]
  public void Sign_PublicKeyIsNullByDefault()
  {
    var keyPair = _provider.GenerateKeyPair();
    var dto = new TestPayload("test", 1);

    var signedDto = _provider.Sign(dto, keyPair.PrivateKey);

    Assert.Null(signedDto.PublicKey);
  }

  [Fact]
  public void VerifyTimestamp_AtExactlyClockSkew_ReturnsTrue()
  {
    var keyPair = _provider.GenerateKeyPair();
    var dto = new TestPayload("test", 1);
    var now = _timeProvider.GetUtcNow();
    var signedDto = _provider.Sign(dto, keyPair.PrivateKey);
    var clockSkew = TimeSpan.FromSeconds(30);

    // Advance time to exactly the clock skew boundary
    _timeProvider.SetUtcNow(now.Add(clockSkew));

    var isValid = _provider.VerifyTimestamp(signedDto, clockSkew);

    Assert.True(isValid);
  }

  [Fact]
  public void VerifyTimestamp_FutureTimestampPastClockSkew_ReturnsFalse()
  {
    var keyPair = _provider.GenerateKeyPair();
    var dto = new TestPayload("test", 1);
    var clockSkew = TimeSpan.FromSeconds(30);

    // Create a signed DTO with a timestamp far in the past, then set clock even further back
    // to simulate a future-dated message arriving
    var signedDto = _provider.Sign(dto, keyPair.PrivateKey);
    var futureDto = signedDto with { Timestamp = signedDto.Timestamp.AddHours(1) };

    var isValid = _provider.VerifyTimestamp(futureDto, clockSkew);

    Assert.False(isValid);
  }

  [Fact]
  public void VerifyTimestamp_PastClockSkew_ReturnsFalse()
  {
    var keyPair = _provider.GenerateKeyPair();
    var dto = new TestPayload("test", 1);
    var now = _timeProvider.GetUtcNow();
    var signedDto = _provider.Sign(dto, keyPair.PrivateKey);
    var clockSkew = TimeSpan.FromSeconds(30);

    // Advance time past the clock skew boundary
    _timeProvider.SetUtcNow(now.Add(clockSkew).Add(TimeSpan.FromTicks(1)));

    var isValid = _provider.VerifyTimestamp(signedDto, clockSkew);

    Assert.False(isValid);
  }

  [Fact]
  public void VerifyTimestamp_WithinClockSkew_ReturnsTrue()
  {
    var keyPair = _provider.GenerateKeyPair();
    var dto = new TestPayload("test", 1);
    var signedDto = _provider.Sign(dto, keyPair.PrivateKey);
    var clockSkew = TimeSpan.FromSeconds(30);

    var isValid = _provider.VerifyTimestamp(signedDto, clockSkew);

    Assert.True(isValid);
  }

  [Fact]
  public void Verify_WithTamperedPayload_Fails()
  {
    var keyPair = _provider.GenerateKeyPair();
    var dto = new TestPayload("original", 1);

    var signedDto = _provider.Sign(dto, keyPair.PrivateKey);
    var tamperedDto = signedDto with { Dto = new TestPayload("tampered", 999) };
    var isValid = _provider.Verify(tamperedDto, keyPair.PublicKey);

    Assert.False(isValid);
  }

  [Fact]
  public void Verify_WithTamperedSignature_Fails()
  {
    var keyPair = _provider.GenerateKeyPair();
    var dto = new TestPayload("original", 1);

    var signedDto = _provider.Sign(dto, keyPair.PrivateKey);
    var tamperedSignature = new byte[signedDto.Signature.Length];
    Array.Copy(signedDto.Signature, tamperedSignature, signedDto.Signature.Length);
    tamperedSignature[0] ^= 1;
    var tamperedDto = signedDto with { Signature = tamperedSignature };
    var isValid = _provider.Verify(tamperedDto, keyPair.PublicKey);

    Assert.False(isValid);
  }

  [Fact]
  public void Verify_WithTamperedTimestamp_Fails()
  {
    var keyPair = _provider.GenerateKeyPair();
    var dto = new TestPayload("test", 1);
    var signedDto = _provider.Sign(dto, keyPair.PrivateKey);
    var replayedDto = signedDto with { Timestamp = signedDto.Timestamp.AddHours(1) };

    var isValid = _provider.Verify(replayedDto, keyPair.PublicKey);

    Assert.False(isValid);
  }

  [Fact]
  public void Verify_WithWrongPublicKey_Fails()
  {
    var keyPair1 = _provider.GenerateKeyPair();
    var keyPair2 = _provider.GenerateKeyPair();
    var dto = new TestPayload("test", 1);

    var signedDto = _provider.Sign(dto, keyPair1.PrivateKey);
    var isValid = _provider.Verify(signedDto, keyPair2.PublicKey);

    Assert.False(isValid);
  }
}

[MessagePackObject(keyAsPropertyName: true, AllowPrivate = true)]
internal record TestPayload(string Message, int Value);
