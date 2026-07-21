using Asp.Versioning;
using Microsoft.AspNetCore.OpenApi;

namespace ControlR.Web.Server.Startup;

public static class OpenApiRegistrationExtensions
{
  public static void AddControlrOpenApi(this IHostApplicationBuilder builder)
  {
    builder.Services
      .AddApiVersioning(options =>
      {
        options.AssumeDefaultVersionWhenUnspecified = true;
        options.DefaultApiVersion = new ApiVersion(0, 0);
        options.ReportApiVersions = true;
      })
      .AddApiExplorer(options => 
      {
        options.GroupNameFormat = "'v'VVV"; 
      })
      .AddMvc()
      .AddOpenApi(options =>
      {
        switch (options.Description.GroupName)
        {
          case "v1":
            options.Document.AddDocumentTransformer<V1ApiDocumentInfoTransformer>();
            break;
          case "Internal":
            options.Document.AddDocumentTransformer<InternalApiDocumentInfoTransformer>();
            break;
          default:
            throw new InvalidOperationException($"Unknown API version/group: {options.Description.GroupName}");
        }
      });

    builder.Services.AddOpenApi("internal", options =>
    {
      options.ShouldInclude = desc => desc.GroupName == "Internal";
      options.AddDocumentTransformer<InternalApiDocumentInfoTransformer>();
      AddSharedTransformers(options);
    });

    builder.Services.AddOpenApi("v1", options =>
    {
      options.ShouldInclude = desc => desc.GroupName == "v1";
      options.AddDocumentTransformer<V1ApiDocumentInfoTransformer>();
      AddSharedTransformers(options);
    });

    builder.Services.AddEndpointsApiExplorer();
  }

  private static void AddSharedTransformers(OpenApiOptions options)
  {
    options.AddDocumentTransformer<FileUploadTransformer>();
    options.AddDocumentTransformer<IdentityApiOpenApiTransformer>();
    options.AddDocumentTransformer<ApiProblemDetailsTransformer>();
    options.AddDocumentTransformer<OpenApiSecurityTransformer>();
    options.AddOperationTransformer<OpenApiSecurityTransformer>();
    options.AddSchemaTransformer<OpenApiSchemaTypeTransformer>();
  }
}