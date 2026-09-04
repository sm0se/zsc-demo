using Zsc.AuditService.Middleware;

var builder = WebApplication.CreateBuilder(args);

var httpPort = int.Parse(builder.Configuration["AuditService:HttpPort"] ?? "5401");
var discoveryUrl = builder.Configuration["ServiceDiscovery:Url"] ?? "http://localhost:5300";

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenLocalhost(httpPort, o => o.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1);
});

builder.Services.AddHttpClient();

var app = builder.Build();

// Register with service discovery on startup
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var httpClientFactory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();
    
    try
    {
        var httpClient = httpClientFactory.CreateClient();
        var registerRequest = new { httpBaseUrl = $"http://localhost:{httpPort}" };
        var response = await httpClient.PostAsJsonAsync($"{discoveryUrl}/services/audit-service/register", registerRequest);
        
        if (response.IsSuccessStatusCode)
        {
            logger.LogInformation("Successfully registered audit-service with ServiceDiscovery at {Url}", discoveryUrl);
        }
        else
        {
            logger.LogWarning("Failed to register audit-service with ServiceDiscovery: {StatusCode}", response.StatusCode);
        }
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Error registering audit-service with ServiceDiscovery at {Url}", discoveryUrl);
    }
}

app.UseCorrelationId();

// Simple audit endpoint - returns a dummy audit record
app.MapGet("/audits/{auditId}", (string auditId, HttpContext context, ILogger<Program> logger) =>
{
    var correlationId = context.GetCorrelationId();
    logger.LogInformation("GET /audits/{AuditId} with correlation ID {CorrelationId}", auditId, correlationId);
    
    return Results.Ok(new AuditRecord(
        auditId: auditId,
        timestamp: DateTimeOffset.UtcNow,
        action: "VIEWED",
        resource: "document",
        details: $"Audit record for ID {auditId}"));
});

app.Run();

public partial class Program { }

public sealed record AuditRecord(string auditId, DateTimeOffset timestamp, string action, string resource, string details);
