using System.Net.Http.Json;
using Zsc.CommonLib.Dtos;
using Zsc.CommonLib.Events;
using Zsc.PatientService.Data;
using Zsc.PatientService.Grpc;

var builder = WebApplication.CreateBuilder(args);

var httpPort = int.Parse(builder.Configuration["Ports:Http"] ?? "5101");
var grpcPort = int.Parse(builder.Configuration["Ports:Grpc"] ?? "5102");

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenLocalhost(httpPort, o => o.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1);
    options.ListenLocalhost(grpcPort, o => o.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2);
});

builder.Services.AddSingleton<IPatientRepository, InMemoryPatientRepository>();
builder.Services.AddSingleton<IEventBus, InMemoryEventBus>();
builder.Services.AddGrpc();
builder.Services.AddHttpClient();

var app = builder.Build();

// Register this service with the discovery service on startup
var discoveryBaseUrl = builder.Configuration["ServiceDiscovery:BaseUrl"] ?? "http://localhost:5300";
var httpClientFactory = app.Services.GetRequiredService<IHttpClientFactory>();
var registrationLogger = app.Services.GetRequiredService<ILogger<Program>>();

_ = Task.Run(async () =>
{
    await Task.Delay(500); // Give the service discovery service time to start
    try
    {
        var client = httpClientFactory.CreateClient();
        var registration = new
        {
            httpBaseUrl = $"http://localhost:{httpPort}",
            grpcAddress = $"http://localhost:{grpcPort}",
            eventTopic = "patient-service-events"
        };
        var response = await client.PostAsJsonAsync($"{discoveryBaseUrl}/services/patient-service/register", registration);
        if (response.IsSuccessStatusCode)
        {
            registrationLogger.LogInformation("Successfully registered patient-service with discovery service");
        }
        else
        {
            registrationLogger.LogError("Failed to register patient-service: {StatusCode}", response.StatusCode);
        }
    }
    catch (Exception ex)
    {
        registrationLogger.LogError(ex, "Error registering patient-service with discovery service");
    }
});

app.MapGrpcService<PatientGrpcService>();

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

app.MapGet("/patients/{patientId}", (string patientId, IPatientRepository repo, HttpContext context, ILogger<Program> logger) =>
{
    var correlationId = context.Items["CorrelationId"];
    logger.LogInformation("GET /patients/{PatientId} [CorrelationId={CorrelationId}]", patientId, correlationId);
    var patient = repo.Get(patientId);
    return patient is null ? Results.NotFound() : Results.Ok(ToDto(patient));
});

app.MapGet("/patients/{patientId}/history", (string patientId, IPatientRepository repo, HttpContext context, ILogger<Program> logger) =>
{
    var correlationId = context.Items["CorrelationId"];
    logger.LogInformation("GET /patients/{PatientId}/history [CorrelationId={CorrelationId}]", patientId, correlationId);
    var patient = repo.Get(patientId);
    if (patient is null) return Results.NotFound();

    var history = patient.History
        .Select(h => new PatientHistoryEntryDto(patient.PatientId, h.OccurredAtUtc, h.Description))
        .ToList();
    return Results.Ok(history);
});

app.MapPost("/patients", async (CreatePatientRequest request, IPatientRepository repo, IEventBus eventBus, HttpContext context, ILogger<Program> logger) =>
{
    var correlationId = context.Items["CorrelationId"];
    logger.LogInformation("POST /patients [CorrelationId={CorrelationId}]", correlationId);
    var patient = repo.Create(request.MedicalRecordNumber, request.DisplayName, request.DateOfBirth);
    await eventBus.PublishAsync(new PatientUpdatedEvent(patient.PatientId, DateTimeOffset.UtcNow, "Patient created"));
    return Results.Created($"/patients/{patient.PatientId}", ToDto(patient));
});

app.MapPost("/patients/{patientId}/history", async (string patientId, PatientHistoryEntryDto entry, IPatientRepository repo, IEventBus eventBus, HttpContext context, ILogger<Program> logger) =>
{
    var correlationId = context.Items["CorrelationId"];
    logger.LogInformation("POST /patients/{PatientId}/history [CorrelationId={CorrelationId}]", patientId, correlationId);
    var patient = repo.AddHistory(patientId, entry.Description);
    if (patient is null) return Results.NotFound();

    await eventBus.PublishAsync(new PatientUpdatedEvent(patient.PatientId, DateTimeOffset.UtcNow, entry.Description));
    return Results.Ok(ToDto(patient));
});

app.Run();

static PatientDto ToDto(Zsc.PatientService.Models.Patient patient) => new(
    patient.PatientId,
    patient.MedicalRecordNumber,
    patient.DisplayName,
    patient.DateOfBirth,
    patient.History.Count);

public partial class Program { }
