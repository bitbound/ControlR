using Bitbound.SimpleMessenger;
using ControlR.Devices.Common.Extensions;
using ControlR.Shared.Collections;
using ControlR.Viewer.Models;
using ControlR.Viewer.Models.Messages;

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

        _messenger.RegisterGenericMessage(this, HandleGenericMessage);
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

    private void HandleGenericMessage(object recipient, GenericMessageKind kind)
    {
        if (kind == GenericMessageKind.HubConnectionStateChanged)
        {
            _cache.Clear();
            _messenger.SendGenericMessage(GenericMessageKind.DeviceContentWindowsChanged);
        }
    }
}