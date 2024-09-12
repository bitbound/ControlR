namespace ControlR.Viewer.Services;

public interface IAppState
{
    CancellationToken AppExiting { get; }

    bool IsAuthenticated { get; }

    bool IsBusy { get; }

    bool IsServerAdministrator { get; internal set; }


    bool IsStoreBuild { get; }
    KeypairState KeypairState { get; }

    bool KeysVerified { get; set; }

    int PendingOperations { get; }

    byte[] PrivateKey { get; }

    string PrivateKeyBase64 { get; }

    byte[] PublicKey { get; }

    string PublicKeyBase64 { get; }

    UserKeyPair UserKeys { get; }
    bool UserKeysPresent { get; }
    Task ClearKeys();
    IDisposable IncrementBusyCounter(Action? additionalDisposedAction = null);
    Task UpdateKeypair(UserKeyPair keypair);
}

internal class AppState(IMessenger _messenger) : IAppState
{
    private static readonly CancellationTokenSource _appExitingCts = new();
    private readonly CancellationToken _appExiting = _appExitingCts.Token;
    private volatile int _busyCounter;
    private byte[] _privateKey = [];
    private byte[] _publicKey = [];
    private UserKeyPair? _userKeys;
    public CancellationToken AppExiting => _appExiting;

    public bool IsAuthenticated => KeypairState == KeypairState.KeysVerified;

    public bool IsBusy => _busyCounter > 0;

    public bool IsServerAdministrator { get; set; }


    public KeypairState KeypairState
    {
        get
        {
            if (!UserKeysPresent)
            {
                return KeypairState.NoKeysPresent;
            }

            if (!KeysVerified)
            {
                return KeypairState.KeysUnverified;
            }

            return KeypairState.KeysVerified;
        }
    }

    public bool KeysVerified { get; set; }
    public int PendingOperations => _busyCounter;
    public byte[] PrivateKey
    {
        get => _privateKey;
        set => _privateKey = value;
    }

    public string PrivateKeyBase64
    {
        get
        {
            try
            {
                if (_privateKey.Length > 0)
                {
                    return Convert.ToBase64String(_privateKey);
                }
            }
            catch { }
            return string.Empty;
        }
    }

    public byte[] PublicKey
    {
        get => _publicKey;
        private set => _publicKey = value;
    }

    public string PublicKeyBase64
    {
        get
        {
            try
            {
                if (_publicKey.Length > 0)
                {
                    return Convert.ToBase64String(_publicKey);
                }
            }
            catch { }
            return string.Empty;
        }
    }

    public UserKeyPair UserKeys => _userKeys ?? throw new InvalidOperationException("User keys not present.");
    public bool UserKeysPresent => _privateKey.Length > 0;

    public bool IsStoreBuild => ViewerConstants.IsStoreBuild;

    public async Task ClearKeys()
    {
        PrivateKey = [];
        PublicKey = [];
        _userKeys = null;
        await _messenger.SendGenericMessage(GenericMessageKind.KeysStateChanged);
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

    public async Task UpdateKeypair(UserKeyPair keypair)
    {
        _userKeys = keypair;
        PublicKey = keypair.PublicKey;
        PrivateKey = keypair.PrivateKey;
        await _messenger.SendGenericMessage(GenericMessageKind.KeysStateChanged);
    }
}