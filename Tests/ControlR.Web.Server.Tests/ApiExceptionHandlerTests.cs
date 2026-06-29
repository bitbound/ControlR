using System.Text.Json;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Web.Server.ExceptionHandlers;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace ControlR.Web.Server.Tests;

public class ApiExceptionHandlerTests
{
  private readonly JsonSerializerOptions _jsonOptions = new()
  {
    PropertyNameCaseInsensitive = true,
  };

  [Fact]
  public async Task TryHandleAsync_ApiPath_ReturnsProblemDetailsWithTraceId()
  {
    var handler = new ApiExceptionHandler(NullLogger<ApiExceptionHandler>.Instance);
    var httpContext = new DefaultHttpContext();
    httpContext.Request.Path = "/api/devices";
    httpContext.TraceIdentifier = "test-trace-123";
    httpContext.Response.Body = new MemoryStream();
    var exception = new InvalidOperationException("Something broke");

    var handled = await handler.TryHandleAsync(httpContext, exception, TestContext.Current.CancellationToken);

    Assert.True(handled);
    Assert.Equal(StatusCodes.Status500InternalServerError, httpContext.Response.StatusCode);
    Assert.StartsWith("application/problem+json", httpContext.Response.ContentType);

    httpContext.Response.Body.Position = 0;
    using var reader = new StreamReader(httpContext.Response.Body);
    var body = await reader.ReadToEndAsync(TestContext.Current.CancellationToken);

    var problem = JsonSerializer.Deserialize<ProblemDetailsDto>(body, _jsonOptions);

    Assert.NotNull(problem);
    Assert.Equal(500, problem.Status);
    Assert.Equal("An unexpected error occurred.", problem.Title);
    Assert.Equal("https://tools.ietf.org/html/rfc9110#section-15.6.1", problem.Type);
    Assert.Equal("An unexpected error occurred.", problem.Detail);
    Assert.NotNull(problem.Extensions);
    Assert.True(problem.Extensions.ContainsKey("traceId"));
    Assert.Equal("test-trace-123", problem.Extensions["traceId"].GetString());
  }

  [Fact]
  public async Task TryHandleAsync_NonApiPathWithJsonAccept_ReturnsProblemDetails()
  {
    var handler = new ApiExceptionHandler(NullLogger<ApiExceptionHandler>.Instance);
    var httpContext = new DefaultHttpContext();
    httpContext.Request.Path = "/some-page";
    httpContext.Request.Headers.Accept = "application/json";
    httpContext.Response.Body = new MemoryStream();
    var exception = new InvalidOperationException("test");

    var handled = await handler.TryHandleAsync(httpContext, exception, TestContext.Current.CancellationToken);

    Assert.True(handled);
  }

  [Fact]
  public async Task TryHandleAsync_NonApiPathWithoutJsonAccept_ReturnsFalse()
  {
    var handler = new ApiExceptionHandler(NullLogger<ApiExceptionHandler>.Instance);
    var httpContext = new DefaultHttpContext();
    httpContext.Request.Path = "/some-page";
    httpContext.Response.Body = new MemoryStream();
    var exception = new InvalidOperationException("test");

    var handled = await handler.TryHandleAsync(httpContext, exception, TestContext.Current.CancellationToken);

    Assert.False(handled);
  }
}
