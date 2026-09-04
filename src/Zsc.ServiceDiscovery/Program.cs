using Zsc.ServiceDiscovery;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenLocalhost(5300, o => o.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1);
});

builder.Services.AddSingleton<IInMemoryRegistry, InMemoryRegistry>();

var app = builder.Build();

// Register a service with its addresses (HTTP, gRPC, event-topic)
app.MapPost("/services/{name}/register", (string name, ServiceRegistration request, IInMemoryRegistry registry, ILogger<Program> logger) =>
{
    if (string.IsNullOrWhiteSpace(request.HttpBaseUrl) && string.IsNullOrWhiteSpace(request.GrpcAddress))
    {
        return Results.BadRequest("At least one of httpBaseUrl or grpcAddress is required.");
    }

    registry.Register(name, request);
    logger.LogInformation("Service '{ServiceName}' registered: HTTP={HttpUrl}, gRPC={GrpcAddress}, EventTopic={EventTopic}",
        name, request.HttpBaseUrl ?? "N/A", request.GrpcAddress ?? "N/A", request.EventTopic ?? "N/A");
    return Results.Ok(new { message = $"Service '{name}' registered successfully." });
});

// Resolve a service by name
app.MapGet("/services/{name}/resolve", (string name, IInMemoryRegistry registry, ILogger<Program> logger) =>
{
    var entry = registry.Resolve(name);
    if (entry is null)
    {
        logger.LogWarning("Service '{ServiceName}' not found in registry.", name);
        return Results.NotFound(new { error = $"Service '{name}' not found." });
    }

    logger.LogInformation("Resolved service '{ServiceName}': HTTP={HttpUrl}, gRPC={GrpcAddress}, EventTopic={EventTopic}",
        name, entry.HttpBaseUrl ?? "N/A", entry.GrpcAddress ?? "N/A", entry.EventTopic ?? "N/A");
    return Results.Ok(entry);
});

app.Run();

public partial class Program { }
