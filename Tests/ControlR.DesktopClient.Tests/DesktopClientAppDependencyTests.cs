using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.DesktopClient.Views;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace ControlR.DesktopClient.Tests;

public class DesktopClientAppDependencyTests
{
  [Theory]
  [InlineData(DesktopEnvironmentType.X11, "Development")]
  [InlineData(DesktopEnvironmentType.Wayland, "Development")]
  [InlineData(DesktopEnvironmentType.X11, "Production")]
  [InlineData(DesktopEnvironmentType.Wayland, "Production")]
  public void Build_ValidatesDependencyGraph(DesktopEnvironmentType desktopEnvironment, string environment)
  {
    switch (desktopEnvironment)
    {
      case DesktopEnvironmentType.X11:
        Environment.SetEnvironmentVariable("DISPLAY", ":0");
        Environment.SetEnvironmentVariable("WAYLAND_DISPLAY", null);
        break;
      case DesktopEnvironmentType.Wayland:
        Environment.SetEnvironmentVariable("WAYLAND_DISPLAY", "wayland-0");
        Environment.SetEnvironmentVariable("DISPLAY", null);
        break;
    }
    Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", environment);

    // So some services don't try to initialize when we resolve views.
    var designModeProperty = typeof(Design).GetProperty(
      nameof(Design.IsDesignMode),
      System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);

    designModeProperty!.SetValue(null, true);
    Assert.True(Design.IsDesignMode);

    var instanceId = $"test-{Guid.NewGuid()}";
    var mockLifetime = new Mock<IControlledApplicationLifetime>();

    StaticServiceProvider.Build(mockLifetime.Object, instanceId);

    try
    {
      var serviceDescriptors = StaticServiceProvider.GetServiceDescriptors();
      using var scope = StaticServiceProvider.Instance.CreateScope();
      var skippedGenericTypes = new List<ServiceDescriptor>();

      foreach (var descriptor in serviceDescriptors)
      {
        if (descriptor.ServiceType.IsGenericType)
        {
          // Skip open generic types as they cannot be resolved directly.
          continue;
        }

        if (descriptor.ImplementationType?.IsAssignableTo(typeof(Window)) == true)
        {
          // Skip Window types as they require an actual Avalonia app running.
          continue;
        }

        var service = scope.ServiceProvider.GetService(descriptor.ServiceType);
        Assert.NotNull(service);
      }

      // Produced by a factory.
      var screenGrabber = scope.ServiceProvider.GetRequiredService<IScreenGrabber>();
      Assert.NotNull(screenGrabber);
    }
    catch (Exception ex)
    {
      Console.WriteLine($"Dependency resolution failed for desktop environment {desktopEnvironment} in {environment} environment: {ex}");
      throw;
    }
    finally
    {
      Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", null);
    }
  }
}

