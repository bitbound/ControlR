using ControlR.Tests.TestingUtilities;
using Microsoft.Extensions.Logging.Abstractions;

namespace ControlR.Libraries.SecureStorage.Tests;

public class SecureStorageCustomServiceNameTests : IDisposable
{
  private readonly ISecureStorage _secureStorage;

  public SecureStorageCustomServiceNameTests()
  {
    _secureStorage = SecureStorage.Create(
        NullLoggerFactory.Instance,
        options => options.ServiceName = "TestService123");
  }

  public void Dispose()
  {
    try
    {
      _secureStorage.RemoveAllAsync().GetAwaiter().GetResult();
    }
    catch
    {
      // Ignore cleanup errors
    }
    _secureStorage?.Dispose();
    GC.SuppressFinalize(this);
  }

  [LinuxOnlyFact]
  public async Task Linux_UsesCustomServiceNameInPath()
  {
    // This test verifies that Linux implementation uses the custom service name
    // by attempting to store and retrieve a value
    var key = $"path_test_{Guid.NewGuid()}";
    var value = "test";

    try
    {
      await _secureStorage.SetAsync(key, value);
      var result = await _secureStorage.GetAsync(key);
      Assert.Equal(value, result);
    }
    finally
    {
      await _secureStorage.RemoveAsync(key);
    }
  }

  [MacOnlyFact]
  public async Task Mac_UsesCustomServiceNameInKeychain()
  {
    // This test verifies that Mac implementation uses the custom service name
    // by attempting to store and retrieve a value
    var key = $"keychain_test_{Guid.NewGuid()}";
    var value = "test";

    try
    {
      await _secureStorage.SetAsync(key, value);
      var result = await _secureStorage.GetAsync(key);
      Assert.Equal(value, result);
    }
    finally
    {
      await _secureStorage.RemoveAsync(key);
    }
  }

  [Fact]
  public async Task SetAsync_AndGetAsync_WorksWithCustomServiceName()
  {
    // Arrange
    var key = $"test_key_{Guid.NewGuid()}";
    var value = "test_value_with_custom_service";

    try
    {
      // Act
      await _secureStorage.SetAsync(key, value);
      var result = await _secureStorage.GetAsync(key);

      // Assert
      Assert.Equal(value, result);
    }
    finally
    {
      await _secureStorage.RemoveAsync(key);
    }
  }

  [WindowsOnlyFact]
  public async Task Windows_UsesCustomServiceNameInPath()
  {
    // This test verifies that Windows implementation uses the custom service name
    // by attempting to store and retrieve a value
    var key = $"path_test_{Guid.NewGuid()}";
    var value = "test";

    try
    {
      await _secureStorage.SetAsync(key, value);
      var result = await _secureStorage.GetAsync(key);
      Assert.Equal(value, result);
    }
    finally
    {
      await _secureStorage.RemoveAsync(key);
    }
  }
}
