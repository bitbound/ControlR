using ControlR.Web.Client.Components;
using ControlR.Web.Client.Enums;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.SignalR.Client;

namespace ControlR.Web.Client.Services;

public interface IDeviceContentWindowStore
{
  IReadOnlyList<DeviceContentInstance> Windows { get; }

  void Add(DeviceContentInstance instance);
  void AddContentInstance<T>(DeviceResponseDto device, DeviceContentInstanceType instanceType, Dictionary<string, object?> componentParams)
     where T : ComponentBase;
  void Remove(DeviceContentInstance instance);
}

internal class DeviceContentWindowStore : IDeviceContentWindowStore
{
  private static readonly ConcurrentList<DeviceContentInstance> _cache = [];
  private readonly IMessenger _messenger;

  public DeviceContentWindowStore(IMessenger messenger)
  {
    _messenger = messenger;

    _messenger.Register<HubConnectionStateChangedMessage>(this, HandleHubConnectionStateChangedMessage);
  }

  public IReadOnlyList<DeviceContentInstance> Windows => _cache;

  public void Add(DeviceContentInstance instance)
  {
    _cache.Add(instance);
    _messenger.SendGenericMessage(EventMessageKind.DeviceContentWindowsChanged);
  }

  public void AddContentInstance<T>(
    DeviceResponseDto device, 
    DeviceContentInstanceType instanceType, 
    Dictionary<string, object?>? componentParams = null)
    where T : ComponentBase
  {
    void RenderComponent(RenderTreeBuilder builder)
    {
      builder.OpenComponent<T>(0);
      
      if (componentParams is not null)
      {
        for (var i = 0; i < componentParams.Count; i++)
        {
          var key = componentParams.Keys.ElementAt(i);
          var value = componentParams[key];

          builder.AddComponentParameter(i + 1, key, value);
        }
      }

      builder.CloseComponent();
    }

    var contentInstance = new DeviceContentInstance(device, RenderComponent, instanceType);
    _cache.Add(contentInstance);
    _messenger.SendGenericMessage(EventMessageKind.DeviceContentWindowsChanged);
  }

  public void Remove(DeviceContentInstance instance)
  {
    _cache.Remove(instance);
    _messenger.SendGenericMessage(EventMessageKind.DeviceContentWindowsChanged);
  }

  private async Task HandleHubConnectionStateChangedMessage(object subscriber, HubConnectionStateChangedMessage message)
  {
    if (message.NewState != HubConnectionState.Connected)
    {
      _cache.RemoveAll(x => x.ContentType != DeviceContentInstanceType.RemoteControl);
      await _messenger.SendGenericMessage(EventMessageKind.DeviceContentWindowsChanged);
    }
  }
}