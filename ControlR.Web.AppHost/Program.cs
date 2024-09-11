var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.ControlR_Web>("controlr-web");

builder.Build().Run();
