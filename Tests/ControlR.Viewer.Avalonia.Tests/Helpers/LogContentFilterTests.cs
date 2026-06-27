using ControlR.Libraries.Viewer.Common.Helpers;

namespace ControlR.Viewer.Avalonia.Tests.Helpers;

public class LogContentFilterTests
{
  [Fact]
  public void Apply_PreservesLineOrder()
  {
    // Arrange
    var contents = "first\nsecond\nthird\nfourth";

    // Act
    var result = LogContentFilter.Apply(contents, "ir");

    // Assert
    Assert.Equal("first\nthird", result);
  }

  [Fact]
  public void Apply_WithCRLFContent_FiltersCorrectly()
  {
    // Arrange
    // Note: Apply splits on \n only, so \r\n files produce lines with trailing \r.
    var contents = "ERROR: something\r\nINFO: all good\r\nERROR: another";

    // Act
    var result = LogContentFilter.Apply(contents, "ERROR");

    // Assert
    // CRLF is normalized to LF before filtering.
    Assert.Equal("ERROR: something\nERROR: another", result);
  }

  [Fact]
  public void Apply_WithEmptyContents_ReturnsEmpty()
  {
    // Arrange
    var contents = string.Empty;

    // Act
    var result = LogContentFilter.Apply(contents, "filter");

    // Assert
    Assert.Empty(result);
  }

  [Fact]
  public void Apply_WithMatchingFilter_ReturnsMatchingLines()
  {
    // Arrange
    var contents = "ERROR: something\nINFO: all good\nERROR: another";

    // Act
    var result = LogContentFilter.Apply(contents, "ERROR");

    // Assert
    Assert.Equal("ERROR: something\nERROR: another", result);
  }

  [Fact]
  public void Apply_WithNoMatchingLines_ReturnsEmptyString()
  {
    // Arrange
    var contents = "line1\nline2\nline3";

    // Act
    var result = LogContentFilter.Apply(contents, "NONEXISTENT");

    // Assert
    Assert.Empty(result);
  }

  [Fact]
  public void Apply_WithNullFilter_ReturnsContentsUnchanged()
  {
    // Arrange
    var contents = "line1\nline2\nline3";

    // Act
    var result = LogContentFilter.Apply(contents, filter: null);

    // Assert
    Assert.Equal(contents, result);
  }

  [Fact]
  public void Apply_WithWhitespaceFilter_ReturnsContentsUnchanged()
  {
    // Arrange
    var contents = "line1\nline2\nline3";

    // Act
    var result = LogContentFilter.Apply(contents, "   ");

    // Assert
    Assert.Equal(contents, result);
  }
}
