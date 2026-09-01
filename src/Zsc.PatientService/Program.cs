using Zsc.CommonLib.Dtos;
using Zsc.CommonLib.Events;
using Zsc.PatientService.Data;
using Zsc.PatientService.Grpc;

var builder = WebApplication.CreateBuilder(args);

// Ports match the hardcoded entry in Zsc.CommonLib.Routing.ServiceRouteMap for
// "patient-service". Nothing enforces that at build time - if this drifts
// from the route map, every caller breaks and nobody finds out until a call
// fails in production.
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenLocalhost(5101, o => o.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1);
    options.ListenLocalhost(5102, o => o.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2);
});

builder.Services.AddSingleton<IPatientRepository, InMemoryPatientRepository>();
builder.Services.AddSingleton<IEventBus, InMemoryEventBus>();
builder.Services.AddGrpc();

var app = builder.Build();

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
