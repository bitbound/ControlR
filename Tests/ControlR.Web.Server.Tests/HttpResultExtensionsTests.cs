using ControlR.Web.Server.Extensions;
using ControlR.Web.Server.Primitives;
using Microsoft.AspNetCore.Mvc;

namespace ControlR.Web.Server.Tests;

public class HttpResultExtensionsTests
{
  [Fact]
  public void ToActionResult_Success_ReturnsNoContent()
  {
    var result = HttpResult.Ok().ToActionResult();

    Assert.IsType<NoContentResult>(result);
  }

  [Fact]
  public void ToActionResultT_Success_ReturnsOkObjectResult()
  {
    var result = HttpResult.Ok(42).ToActionResult();

    var okResult = Assert.IsType<OkObjectResult>(result.Result);
    Assert.Equal(42, okResult.Value);
  }

  [Theory]
  [InlineData(HttpResultErrorCode.NotFound, 404)]
  [InlineData(HttpResultErrorCode.Conflict, 409)]
  [InlineData(HttpResultErrorCode.BadRequest, 400)]
  [InlineData(HttpResultErrorCode.Unauthorized, 401)]
  [InlineData(HttpResultErrorCode.Forbidden, 403)]
  [InlineData(HttpResultErrorCode.ValidationFailed, 400)]
  [InlineData(HttpResultErrorCode.NotImplemented, 501)]
  [InlineData(HttpResultErrorCode.ServiceUnavailable, 503)]
  [InlineData(HttpResultErrorCode.InternalServerError, 500)]
  public void ToActionResult_EachErrorCode_ReturnsCorrectStatusCode(
    HttpResultErrorCode errorCode, int expectedStatusCode)
  {
    var result = HttpResult.Fail(errorCode, "test reason").ToActionResult();

    var objectResult = Assert.IsType<ObjectResult>(result);
    Assert.Equal(expectedStatusCode, objectResult.StatusCode);

    var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
    Assert.Equal(expectedStatusCode, problem.Status);
    Assert.Equal("test reason", problem.Detail);
  }

  [Fact]
  public void ToActionResult_ErrorResponse_ContainsExpectedProblemDetailsFields()
  {
    var result = HttpResult.Fail(HttpResultErrorCode.NotFound, "User not found").ToActionResult();

    var objectResult = Assert.IsType<ObjectResult>(result);
    var problem = Assert.IsType<ProblemDetails>(objectResult.Value);

    Assert.Equal(404, problem.Status);
    Assert.Equal("Not Found", problem.Title);
    Assert.Equal("User not found", problem.Detail);
    Assert.Equal("https://tools.ietf.org/html/rfc9110#section-15.5.4", problem.Type);
    Assert.Null(problem.Instance);
  }

  [Fact]
  public void ToActionResult_WithoutExtensions_InstanceIsNull()
  {
    var result = HttpResult.Fail(HttpResultErrorCode.BadRequest, "invalid").ToActionResult();

    var objectResult = Assert.IsType<ObjectResult>(result);
    var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
    Assert.Null(problem.Instance);
  }

  [Fact]
  public void ToActionResult_WithExtensions_ExtensionsForwarded()
  {
    var result = HttpResult.Fail(
      HttpResultErrorCode.ValidationFailed,
      "Validation failed",
      new() { ["field"] = "email", ["code"] = "duplicate" }
    ).ToActionResult();

    var objectResult = Assert.IsType<ObjectResult>(result);
    var problem = Assert.IsType<ProblemDetails>(objectResult.Value);

    Assert.True(problem.Extensions.ContainsKey("field"));
    Assert.Equal("email", problem.Extensions["field"]);
    Assert.True(problem.Extensions.ContainsKey("code"));
    Assert.Equal("duplicate", problem.Extensions["code"]);
  }

  [Fact]
  public void HttpResult_Extensions_DefaultsToNull()
  {
    var result = HttpResult.Fail(HttpResultErrorCode.NotFound, "reason");
    Assert.Null(result.Extensions);
  }

  [Fact]
  public void HttpResultT_Extensions_DefaultsToNull()
  {
    var result = HttpResult.Fail<int>(HttpResultErrorCode.NotFound, "reason");
    Assert.Null(result.Extensions);
  }

  [Theory]
  [InlineData(HttpResultErrorCode.BadRequest, "https://tools.ietf.org/html/rfc9110#section-15.5.1")]
  [InlineData(HttpResultErrorCode.Unauthorized, "https://tools.ietf.org/html/rfc9110#section-15.5.2")]
  [InlineData(HttpResultErrorCode.Forbidden, "https://tools.ietf.org/html/rfc9110#section-15.5.3")]
  [InlineData(HttpResultErrorCode.NotFound, "https://tools.ietf.org/html/rfc9110#section-15.5.4")]
  [InlineData(HttpResultErrorCode.Conflict, "https://tools.ietf.org/html/rfc9110#section-15.5.10")]
  [InlineData(HttpResultErrorCode.InternalServerError, "https://tools.ietf.org/html/rfc9110#section-15.6.1")]
  [InlineData(HttpResultErrorCode.NotImplemented, "https://tools.ietf.org/html/rfc9110#section-15.6.2")]
  [InlineData(HttpResultErrorCode.ServiceUnavailable, "https://tools.ietf.org/html/rfc9110#section-15.6.4")]
  [InlineData(HttpResultErrorCode.ValidationFailed, "https://tools.ietf.org/html/rfc9110#section-15.5.1")]
  public void ToActionResult_EachErrorCode_ReturnsCorrectTypeUri(
    HttpResultErrorCode errorCode, string expectedType)
  {
    var result = HttpResult.Fail(errorCode, "test").ToActionResult();

    var objectResult = Assert.IsType<ObjectResult>(result);
    var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
    Assert.Equal(expectedType, problem.Type);
  }

  [Fact]
  public void ToActionResultT_Error_ReturnsObjectResultWithProblemDetails()
  {
    var result = HttpResult.Fail<int>(HttpResultErrorCode.Conflict, "conflicted").ToActionResult();

    var objectResult = Assert.IsType<ObjectResult>(result.Result);
    Assert.Equal(409, objectResult.StatusCode);

    var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
    Assert.Equal("Conflict", problem.Title);
    Assert.Equal("conflicted", problem.Detail);
  }

  [Fact]
  public void ToActionResultT_WithExtensions_ExtensionsForwarded()
  {
    var result = HttpResult.Fail<int>(
      HttpResultErrorCode.ValidationFailed,
      "Validation failed",
      new() { ["field"] = "email" }
    ).ToActionResult();

    var objectResult = Assert.IsType<ObjectResult>(result.Result);
    var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
    Assert.True(problem.Extensions.ContainsKey("field"));
    Assert.Equal("email", problem.Extensions["field"]);
  }
}
