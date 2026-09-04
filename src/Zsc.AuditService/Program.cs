using System.Net.Http.Json;

var builder = WebApplication.CreateBuilder(args);

var httpPort = int.Parse(builder.Configuration["Ports:Http"] ?? "5401");

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenLocalhost(httpPort, o => o.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1);
});

builder.Services.AddHttpClient();

var app = builder.Build();

// Register this service with the discovery service on startup
var discoveryBaseUrl = builder.Configuration["ServiceDiscovery:BaseUrl"] ?? "http://localhost:5300";
var httpClientFactory = app.Services.GetRequiredService<IHttpClientFactory>();
var registrationLogger = app.Services.GetRequiredService<ILogger<Program>>();

_ = Task.Run(async () =>
{
    await Task.Delay(500);
    try
    {
        var client = httpClientFactory.CreateClient();
        var registration = new
        {
            httpBaseUrl = $"http://localhost:{httpPort}",
            grpcAddress = (string?)null,
            eventTopic = (string?)null
        };
        var response = await client.PostAsJsonAsync($"{discoveryBaseUrl}/services/audit-service/register", registration);
        if (response.IsSuccessStatusCode)
        {
            registrationLogger.LogInformation("Successfully registered audit-service with discovery service");
        }
        else
        {
            registrationLogger.LogError("Failed to register audit-service: {StatusCode}", response.StatusCode);
        }
    }
    catch (Exception ex)
    {
        registrationLogger.LogError(ex, "Error registering audit-service with discovery service");
    }
});

// Extract correlation ID from request header if present
app.Use(async (context, next) =>
{
    var correlationId = context.Request.Headers.ContainsKey("X-Correlation-Id")
        ? context.Request.Headers["X-Correlation-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString()
        : Guid.NewGuid().ToString();
    
    context.Items["CorrelationId"] = correlationId;
    context.Response.Headers.Add("X-Correlation-Id", correlationId);
    
    await next(context);
});

// GET /audits/{id} - Return a dummy audit record
app.MapGet("/audits/{id}", (string id, HttpContext context, ILogger<Program> logger) =>
{
    var correlationId = context.Items["CorrelationId"];
    logger.LogInformation("GET /audits/{AuditId} [CorrelationId={CorrelationId}]", id, correlationId);
    
    return Results.Ok(new
    {
        auditId = id,
        eventType = "ApiAccess",
        timestamp = DateTime.UtcNow,
        userId = "user-123",
        action = "Read operation",
        resourceType = "Patient",
        resourceId = "pat-000001",
        status = "Success",
        details = "Audit log entry retrieved successfully"
    });
});

// GET /audits - Return a list of dummy audit records
app.MapGet("/audits", (HttpContext context, ILogger<Program> logger) =>
{
    var correlationId = context.Items["CorrelationId"];
    logger.LogInformation("GET /audits [CorrelationId={CorrelationId}]", correlationId);
    
    return Results.Ok(new[]
    {
        new
        {
            auditId = "audit-001",
            eventType = "UserLogin",
            timestamp = DateTime.UtcNow.AddHours(-2),
            userId = "user-123",
            action = "Login",
            resourceType = "System",
            resourceId = "N/A",
            status = "Success",
            details = "User successfully logged in"
        },
        new
        {
            auditId = "audit-002",
            eventType = "DataAccess",
            timestamp = DateTime.UtcNow.AddHours(-1),
            userId = "user-123",
            action = "Read",
            resourceType = "Patient",
            resourceId = "pat-000001",
            status = "Success",
            details = "Patient record accessed"
        }
    });
});

// GET /audits/health - Health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "audit-service" }));

app.Run();

public partial class Program { }
