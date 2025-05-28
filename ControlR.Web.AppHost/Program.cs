using ControlR.Web.ServiceDefaults;

var builder = DistributedApplication.CreateBuilder(args);

var pgUser = builder.AddParameter("PgUser", true);
var pgPassword = builder.AddParameter("PgPassword", true);

var postgres = builder
    .AddPostgres(ServiceNames.Postgres, pgUser, pgPassword, port: 5432)
    .WithLifetime(ContainerLifetime.Persistent)
    .WithDataVolume("controlr-data")
    .ExcludeFromManifest();

var pgHost = postgres.GetEndpoint("tcp");

var web = builder
    .AddProject<Projects.ControlR_Web_Server>(ServiceNames.Controlr, launchProfileName: "https")
    .WithEnvironment("POSTGRES_USER", pgUser)
    .WithEnvironment("POSTGRES_PASSWORD", pgPassword)
    .WithEnvironment("ControlR_POSTGRES_HOST", pgHost)
    .WithReference(postgres)
    .WaitFor(postgres);

var webEndpoint = web.GetEndpoint("http");

builder
  .AddProject<Projects.ControlR_Agent>(ServiceNames.ControlrAgent, "Run")
  .WithEnvironment("AppOptions__ServerUri", webEndpoint)
  .WaitFor(web)
  .ExcludeFromManifest();

builder.Build().Run();
