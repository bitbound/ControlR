using Bitbound.SimpleMessenger;
using ControlR.Devices.Common.Extensions;
using ControlR.Shared.Models;
using ControlR.Viewer.Enums;
using ControlR.Viewer.Models;
using ControlR.Viewer.Models.Messages;
using System.Collections.ObjectModel;

namespace ControlR.Viewer.Services;

public interface IAppState
{
    CancellationToken AppExiting { get; }
    AuthenticationState AuthenticationState { get; }
    bool IsAuthenticated { get; }
    bool IsBusy { get; }
    bool IsServerAdministrator { get; internal set; }
    bool KeysVerified { get; set; }
    int PendingOperations { get; }
    UserKeyPair UserKeys { get; }

    IDisposable IncrementBusyCounter(Action? additionalDisposedAction = null);

    void RemoveUserKeys();

    void SetUserKeys(UserKeyPair userKeys);
}

internal class AppState(
    ISettings _settings,
    IMessenger _messenger) : IAppState
{
    private static readonly CancellationTokenSource _appExitingCts = new();
    private readonly CancellationToken _appExiting = _appExitingCts.Token;
    private volatile int _busyCounter;
    private UserKeyPair? _userKeys;

    public CancellationToken AppExiting => _appExiting;

    public AuthenticationState AuthenticationState
    {
        get
        {
            if (_settings.PublicKey.Length == 0)
            {
                return AuthenticationState.NoKeysPresent;
            }

            if (_userKeys is null)
            {
                return AuthenticationState.LocalKeysStored;
            }

            if (KeysVerified)
            {
                return AuthenticationState.Authenticated;
            }

            return AuthenticationState.PrivateKeyLoaded;
        }
    }

    public bool IsAuthenticated => AuthenticationState == AuthenticationState.Authenticated;
    public bool IsBusy => _busyCounter > 0;
    public bool IsServerAdministrator { get; set; }
    public bool KeysVerified { get; set; }
    public int PendingOperations => _busyCounter;

    public UserKeyPair UserKeys
    {
        get => _userKeys ?? throw new InvalidOperationException("User keypair has not yet been loaded.");
        set => _userKeys = value;
    }

    public IDisposable IncrementBusyCounter(Action? additionalDisposedAction = null)
    {
        Interlocked.Increment(ref _busyCounter);

        _messenger.SendGenericMessage(GenericMessageKind.PendingOperationsChanged);

        return new CallbackDisposable(() =>
        {
            Interlocked.Decrement(ref _busyCounter);
            _messenger.SendGenericMessage(GenericMessageKind.PendingOperationsChanged);

            additionalDisposedAction?.Invoke();
        });
    }

    public void RemoveUserKeys()
    {
        _userKeys = null;
    }

    public void SetUserKeys(UserKeyPair userKeys)
    {
        _userKeys = userKeys;
    }
}