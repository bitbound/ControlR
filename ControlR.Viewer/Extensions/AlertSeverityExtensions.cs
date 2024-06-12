using MudBlazor;

namespace ControlR.Viewer.Extensions;
public static class AlertSeverityExtensions
{
    public static Severity ToMudSeverity(this AlertSeverity severity)
    {
        return severity switch
        {
            AlertSeverity.Information => Severity.Info,
            AlertSeverity.Warning => Severity.Warning,
            AlertSeverity.Error => Severity.Error,
            _ => Severity.Info
        };
    }
}