using System.Text.Json;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace ControlR.Web.Server.Middleware;

public class DeviceGridExceptionHandlerMiddleware(RequestDelegate next, ILogger<DeviceGridExceptionHandlerMiddleware> logger)
{
    private readonly RequestDelegate _next = next;
    private readonly ILogger<DeviceGridExceptionHandlerMiddleware> _logger = logger;

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while processing device grid request: {Url}", context.Request.GetDisplayUrl());
            
            if (context.Response.HasStarted)
            {
                _logger.LogWarning("Response has already started, cannot modify response to handle exception");
                throw; // Re-throw if response has already started
            }
            
            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";
            
            var problemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "An error occurred while processing the device grid request",
                Detail = ex.Message,
                Instance = context.Request.Path
            };
            
            var json = JsonSerializer.Serialize(problemDetails);
            await context.Response.WriteAsync(json);
        }
    }
}
