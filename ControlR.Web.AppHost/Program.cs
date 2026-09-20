using ControlR.Web.ServiceDefaults;

var builder = DistributedApplication.CreateBuilder(args);

var pgUser = builder.AddParameter("PgUser", true);
var pgPassword = builder.AddParameter("PgPassword", true);
var pgDataVolume = builder.AddParameter("PgDataVolume", false);
var volumeName = await pgDataVolume.Resource.GetValueAsync(CancellationToken.None) ?? "controlr-data";

// The dev database lives in the docker-compose volume, and this server mounts it directly.
// Binding 5432 turns a running compose Postgres into a startup failure for this container
// rather than a second writer on the same data directory.
var postgres = builder
    .AddPostgres(ServiceNames.Postgres, pgUser, pgPassword, port: 5432)
    .WithLifetime(ContainerLifetime.Persistent)
    .WithDataVolume(volumeName)
    .ExcludeFromManifest();

var pgEndpoint = postgres.GetEndpoint("tcp");

var web = builder
    .AddProject<Projects.ControlR_Web_Server>(ServiceNames.Controlr, launchProfileName: "https")
    .WithEnvironment("POSTGRES_USER", pgUser)
    .WithEnvironment("POSTGRES_PASSWORD", pgPassword)
    .WithEnvironment("ControlR_POSTGRES_HOST", pgEndpoint.Property(EndpointProperty.Host))
    .WithEnvironment("ControlR_POSTGRES_PORT", pgEndpoint.Property(EndpointProperty.Port))
    .WithReference(postgres)
    .WaitFor(postgres);

var webEndpoint = web.GetEndpoint("http");

var agent = builder
  .AddProject<Projects.ControlR_Agent>(ServiceNames.ControlrAgent, "Run")
  .WithEnvironment("AppOptions__ServerUri", webEndpoint)
  .WaitFor(web)
  .ExcludeFromManifest();

// The agent's desktop client watcher skips launching in debug mode, so the AppHost starts it
// the way the Full Stack IDE profile does. It retries the agent's pipe for 60s before exiting.
builder
  .AddProject<Projects.ControlR_DesktopClient>(ServiceNames.DesktopClient, "Run")
  .WaitFor(agent)
  .ExcludeFromManifest();

builder.Build().Run();
