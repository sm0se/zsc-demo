using Zsc.CommonLib.Dtos;
using Zsc.CommonLib.Events;
using Zsc.PatientService.Data;
using Zsc.PatientService.Grpc;
using Zsc.PatientService.Middleware;

var builder = WebApplication.CreateBuilder(args);

var httpPort = int.Parse(builder.Configuration["PatientService:HttpPort"] ?? "5101");
var grpcPort = int.Parse(builder.Configuration["PatientService:GrpcPort"] ?? "5102");
var discoveryUrl = builder.Configuration["ServiceDiscovery:Url"] ?? "http://localhost:5300";

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

// Register with service discovery on startup
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var httpClientFactory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();
    
    try
    {
        var httpClient = httpClientFactory.CreateClient();
        var registerRequest = new { httpBaseUrl = $"http://localhost:{httpPort}", grpcAddress = $"http://localhost:{grpcPort}" };
        var response = await httpClient.PostAsJsonAsync($"{discoveryUrl}/services/patient-service/register", registerRequest);
        
        if (response.IsSuccessStatusCode)
        {
            logger.LogInformation("Successfully registered patient-service with ServiceDiscovery at {Url}", discoveryUrl);
        }
        else
        {
            logger.LogWarning("Failed to register patient-service with ServiceDiscovery: {StatusCode}", response.StatusCode);
        }
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Error registering patient-service with ServiceDiscovery at {Url}", discoveryUrl);
    }
}

app.UseCorrelationId();

app.MapGrpcService<PatientGrpcService>();

app.MapGet("/patients/{patientId}", (string patientId, IPatientRepository repo) =>
{
    var patient = repo.Get(patientId);
    return patient is null ? Results.NotFound() : Results.Ok(ToDto(patient));
});

app.MapGet("/patients/{patientId}/history", (string patientId, IPatientRepository repo) =>
{
    var patient = repo.Get(patientId);
    if (patient is null) return Results.NotFound();

    var history = patient.History
        .Select(h => new PatientHistoryEntryDto(patient.PatientId, h.OccurredAtUtc, h.Description))
        .ToList();
    return Results.Ok(history);
});

app.MapPost("/patients", async (CreatePatientRequest request, IPatientRepository repo, IEventBus eventBus) =>
{
    var patient = repo.Create(request.MedicalRecordNumber, request.DisplayName, request.DateOfBirth);
    await eventBus.PublishAsync(new PatientUpdatedEvent(patient.PatientId, DateTimeOffset.UtcNow, "Patient created"));
    return Results.Created($"/patients/{patient.PatientId}", ToDto(patient));
});

app.MapPost("/patients/{patientId}/history", async (string patientId, PatientHistoryEntryDto entry, IPatientRepository repo, IEventBus eventBus) =>
{
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
