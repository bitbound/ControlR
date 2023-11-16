using Bitbound.SimpleMessenger;
using ControlR.Shared.Models;
using ControlR.Shared.Primitives;
using ControlR.Viewer.Enums;
using ControlR.Viewer.Extensions;
using ControlR.Viewer.Models.Messages;

namespace ControlR.Viewer.Services;

public interface IAppState
{
    CancellationToken AppExiting { get; }
    AuthenticationState AuthenticationState { get; }
    bool IsAuthenticated { get; }
    bool IsBusy { get; }
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

            return AuthenticationState.PrivateKeyLoaded;
        }
    }

    public bool IsAuthenticated => AuthenticationState == AuthenticationState.PrivateKeyLoaded;
    public bool IsBusy => _busyCounter > 0;
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