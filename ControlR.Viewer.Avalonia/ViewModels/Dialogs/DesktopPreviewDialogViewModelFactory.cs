using Microsoft.Extensions.DependencyInjection;

namespace ControlR.Viewer.Avalonia.ViewModels.Dialogs;

public interface IDesktopPreviewDialogViewModelFactory
{
  IDesktopPreviewDialogViewModel Create(DesktopSession session);
}

public sealed class DesktopPreviewDialogViewModelFactory(IServiceProvider serviceProvider) : IDesktopPreviewDialogViewModelFactory
{
  private readonly IServiceProvider _serviceProvider = serviceProvider;

  public IDesktopPreviewDialogViewModel Create(DesktopSession session)
  {
    return ActivatorUtilities.CreateInstance<DesktopPreviewDialogViewModel>(_serviceProvider, session);
  }
}
