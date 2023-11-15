using Bitbound.SimpleMessenger;
using ControlR.Viewer.Extensions;
using ControlR.Viewer.Models;
using ControlR.Viewer.Models.Messages;
using System.Collections.Concurrent;

namespace ControlR.Viewer.Services;

public interface IDeviceContentWindowStore
{
    IEnumerable<DeviceContentInstance> Windows { get; }

    void AddOrUpdate(DeviceContentInstance instance);

    void Remove(DeviceContentInstance instance);
}

internal class DeviceContentWindowStore(IMessenger _messenger) : IDeviceContentWindowStore
{
    private static readonly ConcurrentDictionary<Guid, DeviceContentInstance> _cache = new();

    public IEnumerable<DeviceContentInstance> Windows => _cache.Values;

    public void AddOrUpdate(DeviceContentInstance instance)
    {
        _cache.AddOrUpdate(instance.WindowId, instance, (k, v) => instance);
        _messenger.SendGenericMessage(GenericMessageKind.DeviceContentWindowsChanged);
    }

    public void Remove(DeviceContentInstance instance)
    {
        if (_cache.Remove(instance.WindowId, out _))
        {
            _messenger.SendGenericMessage(GenericMessageKind.DeviceContentWindowsChanged);
        }
    }
}