using Zsc.ServiceDiscovery;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenLocalhost(5300, o => o.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1);
});

builder.Services.AddSingleton<IServiceRegistry, InMemoryServiceRegistry>();

var app = builder.Build();

app.MapPost("/services/{name}/register", 
    (string name, RegisterServiceRequest request, IServiceRegistry registry, ILogger<Program> logger) =>
    {
        try
        {
            var entry = new ServiceEntry(
                ServiceName: name,
                HttpBaseUrl: request.HttpBaseUrl,
                GrpcAddress: request.GrpcAddress,
                EventTopic: request.EventTopic);
            
            registry.Register(entry);
            logger.LogInformation("Service '{ServiceName}' registered: HTTP={Http} gRPC={Grpc} Events={Events}",
                name, entry.HttpBaseUrl ?? "(none)", entry.GrpcAddress ?? "(none)", entry.EventTopic ?? "(none)");
            
            return Results.Ok(entry);
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning("Failed to register service '{ServiceName}': {Reason}", name, ex.Message);
            return Results.BadRequest(new { error = ex.Message });
        }
    });

app.MapGet("/services/{name}/resolve", 
    (string name, IServiceRegistry registry, ILogger<Program> logger) =>
    {
        var entry = registry.Resolve(name);
        if (entry is null)
        {
            logger.LogWarning("Service '{ServiceName}' not found in registry", name);
            return Results.NotFound(new { error = $"Service '{name}' not registered" });
        }
        
        return Results.Ok(entry);
    });

app.Run();

public partial class Program { }

public sealed record RegisterServiceRequest(
    string? HttpBaseUrl,
    string? GrpcAddress,
    string? EventTopic);
