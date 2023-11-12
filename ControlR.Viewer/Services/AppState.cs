using Bitbound.SimpleMessenger;
using ControlR.Shared;
using ControlR.Shared.Dtos;
using ControlR.Shared.Services;
using ControlR.Viewer.Enums;
using ControlR.Viewer.Extensions;
using ControlR.Viewer.Models;
using ControlR.Viewer.Models.Messages;
using System.Collections.ObjectModel;

namespace ControlR.Viewer.Services;

public interface IAppState
{
    CancellationToken AppExiting { get; }
    AuthenticationState AuthenticationState { get; }
    ObservableCollection<DeviceContentInstance> DeviceContentWindows { get; }
    IEncryptionSession Encryptor { get; }
    bool IsAuthenticated { get; }
    bool IsBusy { get; }

    int PendingOperations { get; }

    PublicKeyDto GetPublicKeyDto();

    IDisposable IncrementBusyCounter(Action? additionalDisposedAction = null);
}

internal class AppState : IAppState
{
    private static readonly CancellationTokenSource _appExitingCts = new();
    private readonly CancellationToken _appExiting = _appExitingCts.Token;
    private readonly IMessenger _messenger;
    private readonly ISettings _settings;
    private volatile int _busyCounter;

    public AppState(
        ISettings settings,
        IEncryptionSessionFactory encryptionFactory,
        IMessenger messenger)
    {
        _settings = settings;
        _messenger = messenger;
        _messenger.RegisterGenericMessage(this, GenericMessageKind.ShuttingDown, HandleShutdown);
        Encryptor = encryptionFactory.CreateSession();
    }

    public CancellationToken AppExiting => _appExiting;

    public AuthenticationState AuthenticationState
    {
        get
        {
            if (_settings.PublicKey.Length == 0)
            {
                return AuthenticationState.NoKeysPresent;
            }

            if (Encryptor.CurrentState is null)
            {
                return AuthenticationState.LocalKeysStored;
            }

            return AuthenticationState.PrivateKeyLoaded;
        }
    }

    public ObservableCollection<DeviceContentInstance> DeviceContentWindows { get; } = new();

    public IEncryptionSession Encryptor { get; }
    public bool IsAuthenticated => AuthenticationState == AuthenticationState.PrivateKeyLoaded;
    public bool IsBusy => _busyCounter > 0;
    public int PendingOperations => _busyCounter;

    public PublicKeyDto GetPublicKeyDto()
    {
        return new PublicKeyDto()
        {
            PublicKey = _settings.PublicKey,
            Username = _settings.Username
        };
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

    private void HandleShutdown()
    {
        Encryptor.Dispose();
    }
}