using ControlR.Libraries.SecureStorage;
using Microsoft.Extensions.Logging.Abstractions;

namespace ControlR.Libraries.SecureStorage.Tests;

public class SecureStorageTests : IDisposable
{
  private readonly ISecureStorage _secureStorage;

  public SecureStorageTests()
  {
    _secureStorage = SecureStorage.Create(NullLoggerFactory.Instance);
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

  [Fact]
  public async Task GetAsync_ReturnsNull_WhenKeyDoesNotExist()
  {
    // Arrange
    var key = $"test_key_{Guid.NewGuid()}";

    // Act
    var result = await _secureStorage.GetAsync(key);

    // Assert
    Assert.Null(result);
  }

  [Fact]
  public async Task GetAsync_ThrowsArgumentException_WhenKeyIsEmpty()
  {
    // Act & Assert
    await Assert.ThrowsAsync<ArgumentException>(() => _secureStorage.GetAsync(""));
  }

  [Fact]
  public async Task GetAsync_ThrowsArgumentException_WhenKeyIsWhitespace()
  {
    // Act & Assert
    await Assert.ThrowsAsync<ArgumentException>(() => _secureStorage.GetAsync("   "));
  }

  [Fact]
  public async Task RemoveAll_RemovesAllKeys()
  {
    // Arrange
    var key1 = $"test_key_1_{Guid.NewGuid()}";
    var key2 = $"test_key_2_{Guid.NewGuid()}";
    await _secureStorage.SetAsync(key1, "value1");
    await _secureStorage.SetAsync(key2, "value2");

    // Act
    await _secureStorage.RemoveAllAsync();
    var result1 = await _secureStorage.GetAsync(key1);
    var result2 = await _secureStorage.GetAsync(key2);

    // Assert
    Assert.Null(result1);
    Assert.Null(result2);
  }

  [Fact]
  public async Task Remove_RemovesExistingKey()
  {
    // Arrange
    var key = $"test_key_{Guid.NewGuid()}";
    var value = "test_value";
    await _secureStorage.SetAsync(key, value);

    // Act
    var removed = await _secureStorage.RemoveAsync(key);
    var result = await _secureStorage.GetAsync(key);

    // Assert
    Assert.True(removed);
    Assert.Null(result);
  }

  [Fact]
  public async Task Remove_ReturnsFalse_WhenKeyDoesNotExist()
  {
    // Arrange
    var key = $"test_key_{Guid.NewGuid()}";

    // Act
    var removed = await _secureStorage.RemoveAsync(key);

    // Assert
    Assert.False(removed);
  }

  [Fact]
  public async Task SetAsync_AndGetAsync_HandlesConcurrentOperations()
  {
    // Arrange
    var tasks = new List<Task>();
    var keys = new List<string>();

    for (var i = 0; i < 10; i++)
    {
      var key = $"test_key_{Guid.NewGuid()}";
      keys.Add(key);
      var value = $"value_{i}";
      tasks.Add(_secureStorage.SetAsync(key, value));
    }

    try
    {
      // Act
      await Task.WhenAll(tasks);

      // Assert - verify all values were stored
      for (var i = 0; i < keys.Count; i++)
      {
        var result = await _secureStorage.GetAsync(keys[i]);
        Assert.Equal($"value_{i}", result);
      }
    }
    finally
    {
      foreach (var key in keys)
      {
        await _secureStorage.RemoveAsync(key);
      }
    }
  }

  [Fact]
  public async Task SetAsync_AndGetAsync_HandlesLargeValues()
  {
    // Arrange
    var key = $"test_key_{Guid.NewGuid()}";
    var value = new string('A', 10000); // 10KB string

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

  [Fact]
  public async Task SetAsync_AndGetAsync_HandlesSpecialCharacters()
  {
    // Arrange
    var key = $"test_key_{Guid.NewGuid()}";
    var value = "特殊字符 !@#$%^&*()_+{}|:\"<>?[];',./`~";

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

  [Fact]
  public async Task SetAsync_AndGetAsync_StoresAndRetrievesValue()
  {
    // Arrange
    var key = $"test_key_{Guid.NewGuid()}";
    var value = "test_value_12345";

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

  [Fact]
  public async Task SetAsync_ThrowsArgumentException_WhenKeyIsEmpty()
  {
    // Act & Assert
    await Assert.ThrowsAsync<ArgumentException>(() => _secureStorage.SetAsync("", "value"));
  }

  [Fact]
  public async Task SetAsync_ThrowsArgumentException_WhenKeyIsWhitespace()
  {
    // Act & Assert
    await Assert.ThrowsAsync<ArgumentException>(() => _secureStorage.SetAsync("   ", "value"));
  }

  [Fact]
  public async Task SetAsync_ThrowsArgumentNullException_WhenValueIsNull()
  {
    // Act & Assert
    await Assert.ThrowsAsync<ArgumentNullException>(() => _secureStorage.SetAsync("key", null!));
  }

  [Fact]
  public async Task SetAsync_UpdatesExistingValue()
  {
    // Arrange
    var key = $"test_key_{Guid.NewGuid()}";
    var value1 = "initial_value";
    var value2 = "updated_value";

    try
    {
      // Act
      await _secureStorage.SetAsync(key, value1);
      await _secureStorage.SetAsync(key, value2);
      var result = await _secureStorage.GetAsync(key);

      // Assert
      Assert.Equal(value2, result);
    }
    finally
    {
      await _secureStorage.RemoveAsync(key);
    }
  }
}
