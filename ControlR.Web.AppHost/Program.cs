using ControlR.Web.ServiceDefaults;

var builder = DistributedApplication.CreateBuilder(args);

var pgUser = builder.AddParameter("PgUser", true);
var pgPassword = builder.AddParameter("PgPassword", true);

var postgres = builder
    .AddPostgres(ServiceNames.Postgres, pgUser, pgPassword)
    .WithDataVolume("controlr-data");

builder
    .AddProject<Projects.ControlR_Web_Server>(ServiceNames.Controlr)
    .WithReference(postgres);

builder.Build().Run();
