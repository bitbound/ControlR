namespace ControlR.Libraries.SecureStorage.Tests;

public class SecureStorageOptionsTests
{
  [Fact]
  public void ServiceName_AcceptsValidAlphanumericName()
  {
    // Arrange
    var options = new SecureStorageOptions
    {
      // Act
      ServiceName = "MyService123"
    };

    // Assert
    Assert.Equal("MyService123", options.ServiceName);
  }

  [Fact]
  public void ServiceName_DefaultValue_IsControlR()
  {
    // Arrange & Act
    var options = new SecureStorageOptions();

    // Assert
    Assert.Equal("ControlR", options.ServiceName);
  }

  [Fact]
  public void ServiceName_ThrowsForEmptyString()
  {
    // Arrange
    var options = new SecureStorageOptions();

    // Act & Assert
    Assert.Throws<ArgumentException>(() => options.ServiceName = "");
  }

  [Fact]
  public void ServiceName_ThrowsForNameWithSpaces()
  {
    // Arrange
    var options = new SecureStorageOptions();

    // Act & Assert
    Assert.Throws<ArgumentException>(() => options.ServiceName = "My Service");
  }

  [Fact]
  public void ServiceName_ThrowsForNameWithSpecialCharacters()
  {
    // Arrange
    var options = new SecureStorageOptions();

    // Act & Assert
    Assert.Throws<ArgumentException>(() => options.ServiceName = "My-Service");
    Assert.Throws<ArgumentException>(() => options.ServiceName = "My_Service");
    Assert.Throws<ArgumentException>(() => options.ServiceName = "My.Service");
    Assert.Throws<ArgumentException>(() => options.ServiceName = "My@Service");
  }

  [Fact]
  public void ServiceName_ThrowsForNull()
  {
    // Arrange
    var options = new SecureStorageOptions();

    // Act & Assert
    Assert.Throws<ArgumentException>(() => options.ServiceName = null!);
  }

  [Fact]
  public void ServiceName_ThrowsForWhitespace()
  {
    // Arrange
    var options = new SecureStorageOptions();

    // Act & Assert
    Assert.Throws<ArgumentException>(() => options.ServiceName = "   ");
  }
}
