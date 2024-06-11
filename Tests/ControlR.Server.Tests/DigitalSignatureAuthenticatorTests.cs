using ControlR.Server.Auth;
using Microsoft.Extensions.DependencyInjection;
using ControlR.Libraries.Shared.Dtos;
using MessagePack;
using ControlR.Server.Options;
using Microsoft.AspNetCore.Authentication;
using ControlR.Server.Extensions;
using Microsoft.Extensions.Options;
using ControlR.Server.Tests.TestableServices;
using ControlR.Libraries.Shared;
using ControlR.Libraries.Shared.Services.Testable;
using ControlR.Libraries.Shared.Services;

namespace ControlR.Server.Tests;

[TestClass]
public class DigitalSignatureAuthenticatorTests
{
    private readonly TestableSystemTime _systemTime = new();
    private readonly TestableOptionsMonitor<ApplicationOptions> _appOptions = new();
    private ServiceProvider _provider = null!;
    private IKeyProvider _keyProvider = null!;
    private IDigitalSignatureAuthenticator _authenticator = null!;

    [TestInitialize]
    public void Init()
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddSingleton<IKeyProvider, KeyProvider>();
        services.AddSingleton<ISystemTime>(_systemTime);
        services.AddSingleton<IOptionsMonitor<ApplicationOptions>>(_appOptions);
        services.AddTransient<IDigitalSignatureAuthenticator, DigitalSignatureAuthenticator>();

        _provider = services.BuildServiceProvider();
        _keyProvider = _provider.GetRequiredService<IKeyProvider>();
        _authenticator = _provider.GetRequiredService<IDigitalSignatureAuthenticator>();
    }

    [TestMethod]
    public async Task Authenticate_GivenNormalScenario_Succeeds()
    {
        var viewer = _keyProvider.GenerateKeys();

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
        var viewer = _keyProvider.GenerateKeys();

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
        var viewer = _keyProvider.GenerateKeys();
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
        var viewer = _keyProvider.GenerateKeys();

        var dto = new IdentityDto()
        {
            Username = "Tom"
        };

        var signedDto = _keyProvider.CreateSignedDto(dto, DtoType.IdentityAttestation, viewer.PrivateKey);
        var base64Dto = Convert.ToBase64String(MessagePackSerializer.Serialize(signedDto));
        var authHeader = $"{AuthSchemes.DigitalSignature} {base64Dto}";

        _systemTime.AdjustBy(TimeSpan.FromSeconds(30));

        var result = await _authenticator.Authenticate(authHeader);
        Assert.IsTrue(result.Succeeded);
    }

    [TestMethod]
    public async Task Authenticate_WhenRestrictedUserAccessIsEnabled_OnlyAllowsListedUsersAndAdmins()
    {
        var adminViewer = _keyProvider.GenerateKeys();
        var allowedViewer = _keyProvider.GenerateKeys();
        var disallowedViewer = _keyProvider.GenerateKeys();

        var appOptions = new ApplicationOptions()
        {
            EnableRestrictedUserAccess = true,
            AuthorizedUserPublicKeys =
            [
                Convert.ToBase64String(allowedViewer.PublicKey)
            ],
            AdminPublicKeys = 
            [
                Convert.ToBase64String(adminViewer.PublicKey)
            ]
        };

        _appOptions.Set(appOptions);

        var dto = new IdentityDto()
        {
            Username = ""
        };

        async Task<AuthenticateResult> GetResult(byte[] privateKey)
        {
            var signedDto = _keyProvider.CreateSignedDto(dto, DtoType.IdentityAttestation, privateKey);
            var base64Dto = Convert.ToBase64String(MessagePackSerializer.Serialize(signedDto));
            var authHeader = $"{AuthSchemes.DigitalSignature} {base64Dto}";
            return await _authenticator.Authenticate(authHeader);
        }

        var result = await GetResult(disallowedViewer.PrivateKey);
        Assert.IsFalse(result.Succeeded);

        result = await GetResult(allowedViewer.PrivateKey);
        Assert.IsTrue(result.Succeeded);
        Assert.IsFalse(result.Principal.IsAdministrator());

        result = await GetResult(adminViewer.PrivateKey);
        Assert.IsTrue(result.Succeeded);
        Assert.IsTrue(result.Principal.IsAdministrator());
    }
}