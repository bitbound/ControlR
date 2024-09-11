using Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

//var postgres = builder.AddContainer("postgres", "postgres:latest")
//    .WithEnvironment("POSTGRES_DB", "controlr")
//    .WithEnvironment("POSTGRES_USER", "postgres")
//    .WithEnvironment("POSTGRES_PASSWORD", "password")
//    .WithEndpoint(name: "postgresql", port: 5432)
//    .WithBindMount("postgres-data", "/var/lib/postgresql/data");

//var pgEndpoint = postgres.GetEndpoint("postgres");

var postgres = builder
    .AddPostgres("PostgreSQL")
    .WithDataVolume("controlr-data");

builder
    .AddProject<Projects.ControlR_Web>("controlr-web")
    .WithReference(postgres);

builder.Build().Run();
