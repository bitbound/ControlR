using ControlR.Libraries.Shared.Dtos;
using ControlR.Libraries.Shared.Services;
using ControlR.Libraries.Shared.Services.Testable;
using Microsoft.Extensions.Logging;

namespace ControlR.Libraries.Shared.Tests;

[TestClass]
public class KeyProviderTests
{
    private LoggerFactory? _loggerFactory;
    private TestableSystemTime? _systemTime;
    private ILogger<KeyProvider>? _logger;
    private KeyProvider? _keyProvider;

    [TestInitialize]
    public void Init()
    {
        _systemTime = new TestableSystemTime();
        _loggerFactory = new LoggerFactory();
        _logger = _loggerFactory.CreateLogger<KeyProvider>();
        _keyProvider = new KeyProvider(_systemTime, _logger);
    }

    [TestMethod]
    public void Verify_GivenNormalScenario_ReturnsTrue()
    {
        var viewer = _keyProvider!.GenerateKeys();

        var dto = new IdentityDto()
        {
            Username = "Tom"
        };

       var signedDto = _keyProvider.CreateSignedDto(dto, DtoType.IdentityAttestation, viewer.PrivateKey); 

        var result = _keyProvider.Verify(signedDto);
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Verify_WhenPublicKeyIsTamperedWith_ReturnsFalse()
    {
        var viewer = _keyProvider!.GenerateKeys();
        var fakeViewer = _keyProvider.GenerateKeys();

        var dto = new IdentityDto()
        {
            Username = "Tom"
        };

        var signedDto = _keyProvider.CreateSignedDto(dto, DtoType.IdentityAttestation, fakeViewer.PrivateKey);

        typeof(SignedPayloadDto)
            .GetProperty(nameof(SignedPayloadDto.PublicKey))!
            .SetValue(signedDto, viewer.PublicKey);

        var result = _keyProvider.Verify(signedDto);
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Verify_WhenDtoHasExpired_ReturnsFalse()
    {
        var viewer = _keyProvider!.GenerateKeys();

        var dto = new ClipboardChangeDto("some text");

        var signedDto = _keyProvider.CreateSignedDto(dto, DtoType.ClipboardChanged, viewer.PrivateKey);

        _systemTime!.AdjustBy(TimeSpan.FromSeconds(30));

        var result = _keyProvider.Verify(signedDto);
        Assert.IsFalse(result);
    }

    [TestMethod]
    // Identity attestation is not subject to expiration.
    public void Verify_WhenDtoHasExpired_ReturnsTrueForIdentity()
    {
        var viewer = _keyProvider!.GenerateKeys();

        var dto = new IdentityDto()
        {
            Username = "Tom"
        };

        var signedDto = _keyProvider.CreateSignedDto(dto, DtoType.IdentityAttestation, viewer.PrivateKey);

        _systemTime!.AdjustBy(TimeSpan.FromSeconds(30));

        var result = _keyProvider.Verify(signedDto);
        Assert.IsTrue(result);
    }
}