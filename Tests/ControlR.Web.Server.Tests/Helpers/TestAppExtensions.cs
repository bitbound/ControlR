using ControlR.Web.Server.Data.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;

namespace ControlR.Web.Server.Tests.Helpers;

public static class TestAppExtensions
{    
  /// <summary>
  /// Creates an instance of a controller with the necessary services injected from the TestApp
  /// </summary>
  /// <typeparam name="T">The controller type to create</typeparam>
  /// <param name="testApp">The test application instance</param>
  /// <returns>An instance of the controller</returns>
  public static T CreateController<T>(this TestApp testApp) where T : ControllerBase
  {
    var controller = ActivatorUtilities.CreateInstance<T>(testApp.Services);
    controller.ControllerContext = new ControllerContext
    {
      HttpContext = new DefaultHttpContext
      {
        RequestServices = testApp.Services
      }
    };
    return controller;
  }
}
