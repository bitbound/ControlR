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

internal class DeviceContentWindowStore(IMessenger _messenger) : IDeviceContentWindowStore
{
    private static readonly ConcurrentList<DeviceContentInstance> _cache = [];

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
}