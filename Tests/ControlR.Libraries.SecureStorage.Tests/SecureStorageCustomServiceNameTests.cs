using ControlR.Tests.TestingUtilities;
using Microsoft.Extensions.Logging.Abstractions;

namespace ControlR.Libraries.SecureStorage.Tests;

public class SecureStorageCustomServiceNameTests : IAsyncDisposable
{
  private readonly ISecureStorage _secureStorage;

  public SecureStorageCustomServiceNameTests()
  {
    _secureStorage = SecureStorage.Create(
        NullLoggerFactory.Instance,
        options => options.ServiceName = "TestService123");
  }

  public async ValueTask DisposeAsync()
  {
    try
    {
      await _secureStorage.RemoveAllAsync();
    }
    catch
    {
      // Ignore cleanup errors
    }
    _secureStorage?.Dispose();
    GC.SuppressFinalize(this);
  }

  [MacKeychainIntegrationFact]
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
}
