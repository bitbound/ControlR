using ControlR.Shared.Services.Testable;
using ControlR.Shared.Services;
using Microsoft.Extensions.Logging;
using ControlR.Server.Auth;
using Microsoft.Extensions.DependencyInjection;
using ControlR.Shared.Dtos;
using MessagePack;
using ControlR.Shared;

namespace ControlR.Server.Tests;

[TestClass]
public class DigitalSignatureAuthenticatorTests
{
    private TestableSystemTime _systemTime = new();
    private ServiceProvider? _provider;
    private IKeyProvider? _keyProvider;
    private IDigitalSignatureAuthenticator _authenticator;

    [TestInitialize]
    public void Init()
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddSingleton<IKeyProvider, KeyProvider>();
        services.AddSingleton<ISystemTime>(_systemTime);
        services.AddTransient<IDigitalSignatureAuthenticator, DigitalSignatureAuthenticator>();

        _provider = services.BuildServiceProvider();
        _keyProvider = _provider.GetRequiredService<IKeyProvider>();
        _authenticator = _provider.GetRequiredService<IDigitalSignatureAuthenticator>();
    }

    [TestMethod]
    public async Task Authenticate_GivenNormalScenario_Succeeds()
    {
        var viewer = _keyProvider!.GenerateKeys();

        var dto = new IdentityDto()
        {
            Username = "Tom"
        };

        var signedDto = _keyProvider.CreateSignedDto(dto, DtoType.IdentityAttestation, viewer.PrivateKey);
        var base64Dto = Convert.ToBase64String(MessagePackSerializer.Serialize(signedDto));
        var authHeader = $"{AuthSchemes.DigitalSignature} {base64Dto}";
        var result = await _authenticator.Authenticate(authHeader);
        Assert.IsTrue(result.Succeeded);
    }

    [TestMethod]
    public async Task Authenticate_WhenAuthHeaderIsEmpty_Fails()
    {
        var result = await _authenticator.Authenticate(null);
        Assert.IsFalse(result.Succeeded);
        result = await _authenticator.Authenticate("");
        Assert.IsFalse(result.Succeeded);
        result = await _authenticator.Authenticate("    ");
        Assert.IsFalse(result.Succeeded);
    }

    [TestMethod]
    public async Task Authenticate_WhenWrongDtoTypeIsUsed_Fails()
    {
        var viewer = _keyProvider!.GenerateKeys();

        var dto = new ClipboardChangeDto("some text");

        var signedDto = _keyProvider.CreateSignedDto(dto, DtoType.IdentityAttestation, viewer.PrivateKey);
        var base64Dto = Convert.ToBase64String(MessagePackSerializer.Serialize(signedDto));
        var authHeader = $"{AuthSchemes.DigitalSignature} {base64Dto}";
        var result = await _authenticator.Authenticate(authHeader);
        Assert.IsFalse(result.Succeeded);
    }

    [TestMethod]
    public async Task Authenticate_WhenPublicKeyIsTamperedWith_Fails()
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

        var base64Dto = Convert.ToBase64String(MessagePackSerializer.Serialize(signedDto));
        var authHeader = $"{AuthSchemes.DigitalSignature} {base64Dto}";
        var result = await _authenticator.Authenticate(authHeader);
        Assert.IsFalse(result.Succeeded);
        Assert.AreEqual("Digital signature verification failed.", result.Failure!.Message);
    }

    [TestMethod]
    // Identity attestation is not subject to expiration.
    public async Task Authenticate_WhenDtoHasExpired_Succeeds()
    {
        var viewer = _keyProvider!.GenerateKeys();
        var fakeViewer = _keyProvider.GenerateKeys();

        var dto = new IdentityDto()
        {
            Username = "Tom"
        };

        var signedDto = _keyProvider.CreateSignedDto(dto, DtoType.IdentityAttestation, fakeViewer.PrivateKey);
        var base64Dto = Convert.ToBase64String(MessagePackSerializer.Serialize(signedDto));
        var authHeader = $"{AuthSchemes.DigitalSignature} {base64Dto}";

        _systemTime.AdjustBy(TimeSpan.FromSeconds(30));

        var result = await _authenticator.Authenticate(authHeader);
        Assert.IsTrue(result.Succeeded);
    }
}