#if WINDOWS
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using System.Reflection;

namespace ControlR.Viewer.Services.Windows;

public class PointerIconManagerWindows
{
    public Task SetIcon(UIElement element, byte[] iconBitmap)
    {
        
        var cursor = InputSystemCursor.Create(InputSystemCursorShape.Hand);
        var type =
        typeof(UIElement).InvokeMember(
            "ProtectedCursor", 
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.SetProperty | BindingFlags.Instance,
            null,
            element, 
            [cursor]);
        return Task.CompletedTask;
    }
}
#endif