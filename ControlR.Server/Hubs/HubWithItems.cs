using Microsoft.AspNetCore.SignalR;
using System.Runtime.CompilerServices;

namespace ControlR.Server.Hubs;

public abstract class HubWithItems<T> : Hub<T> 
    where T : class
{
    protected TItem GetItem<TItem>(TItem defaultValue, [CallerMemberName] string key = "")
    {
        if (Context.Items.TryGetValue(key, out var cached) &&
            cached is TItem item)
        {
            return item;
        }
        return defaultValue;
    }

    protected void SetItem<TItem>(TItem item, [CallerMemberName] string key = "")
    {
        Context.Items[key] = item;
    }
}
