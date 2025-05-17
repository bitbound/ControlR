using ControlR.Tests.TestingUtilities;
using ControlR.Web.Server.Data;
using ControlR.Web.Server.Data.Entities;
using ControlR.Web.Server.Services;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Xunit.Abstractions;

namespace ControlR.Web.Server.Tests;

public class DeviceManagerTests(ITestOutputHelper testOutput)
{

  private readonly ITestOutputHelper _testOutputHelper = testOutput;

  [Fact]
  public async Task DeviceManager_AddOrUpdate()
  {
    // Arrange
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);
    var deviceManager = testApp.App.Services.GetRequiredService<IDeviceManager>();
    var userManager = testApp.App.Services.GetRequiredService<UserManager<AppUser>>();
    using var db = testApp.App.Services.GetRequiredService<AppDb>();

    // Act
    

    // Assert
    
  }
}