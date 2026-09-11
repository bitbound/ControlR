using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace ControlR.Web.Server.Tests.Helpers;

/// <summary>
/// Assertions for controller action results. Manager-backed failures surface as
/// ProblemDetails <see cref="ObjectResult"/> instances (status carried in StatusCode),
/// while handler-generated shortcuts (Forbid, direct NotFound collapse) are bare result
/// types. Assert intent through the status code, not the concrete class.
/// </summary>
internal static class ActionResultAsserts
{
  internal static void AssertHttpStatus(object? result, int statusCode)
  {
    var objectResult = Assert.IsType<ObjectResult>(result);
    Assert.Equal(statusCode, objectResult.StatusCode);
  }
}
