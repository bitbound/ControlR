using Microsoft.AspNetCore.SignalR.Client;

namespace ControlR.Viewer.Services;

public interface IDeviceContentWindowStore
{
    IEnumerable<DeviceContentInstance> Windows { get; }

    void Add(DeviceContentInstance instance);

    void Remove(DeviceContentInstance instance);
}

internal class DeviceContentWindowStore: IDeviceContentWindowStore
{
    private static readonly ConcurrentList<DeviceContentInstance> _cache = [];
    private readonly IMessenger _messenger;

    public DeviceContentWindowStore(IMessenger messenger)
    {
        _messenger = messenger;

        _messenger.Register<HubConnectionStateChangedMessage>(this, HandleHubConnectionStateChangedMessage);
    }

    public IEnumerable<DeviceContentInstance> Windows => _cache;

    public void Add(DeviceContentInstance instance)
    {
        _cache.Add(instance);
        _messenger.SendGenericMessage(GenericMessageKind.DeviceContentWindowsChanged);
    }

    public void Remove(DeviceContentInstance instance)
    {
        _cache.Remove(instance);
        _messenger.SendGenericMessage(GenericMessageKind.DeviceContentWindowsChanged);
    }

    private async Task HandleHubConnectionStateChangedMessage(object subscriber, HubConnectionStateChangedMessage message)
    {
        if (message.NewState != HubConnectionState.Connected)
        {
            _cache.Clear();
            await _messenger.SendGenericMessage(GenericMessageKind.DeviceContentWindowsChanged);
        }
    }
}