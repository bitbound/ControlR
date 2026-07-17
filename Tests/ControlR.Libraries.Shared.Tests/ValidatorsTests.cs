using ControlR.Libraries.Shared.Constants;
using ControlR.Libraries.Shared.DataValidation;

namespace ControlR.Libraries.Shared.Tests;

public class ValidatorsTests
{
  [Theory]
  [InlineData("default")]
  [InlineData("DEFAULT")]
  [InlineData("DeFaUlT")]
  public void ValidateInstanceId_WhenDefaultIsUsed_ReturnsReservedMessage(string instanceId)
  {
    var result = Validators.ValidateInstanceId(instanceId);

    Assert.Equal($"Instance ID '{AppConstants.DefaultInstanceId}' is reserved.", result);
  }

  [Theory]
  [InlineData(".")]
  [InlineData("..")]
  [InlineData("subdir/name")]
  [InlineData("subdir\\name")]
  [InlineData("name:with:colon")]
  [InlineData("space notallowed")]
  [InlineData("bad*char")]
  public void ValidateInstanceId_WhenInvalidPathSegment_ReturnsError(string instanceId)
  {
    var result = Validators.ValidateInstanceId(instanceId);

    Assert.False(string.IsNullOrEmpty(result));
  }

  [Theory]
  [InlineData("server-alpha")]
  [InlineData("default01")]
  [InlineData("a.b-c_1")]
  [InlineData(null)]
  [InlineData("   ")]
  public void ValidateInstanceId_WhenValueIsAllowed_ReturnsNull(string? instanceId)
  {
    var result = Validators.ValidateInstanceId(instanceId);

    Assert.Null(result);
  }
}