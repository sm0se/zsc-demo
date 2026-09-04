using System.Net;
using Zsc.CommonLib;
using Zsc.CommonLib.Dtos;
using Zsc.Bff.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient("interceptor", client =>
{
    var baseUrl = builder.Configuration["Interceptor:BaseUrl"] ?? "http://localhost:5200";
    client.BaseAddress = new Uri(baseUrl);
});

var app = builder.Build();

app.UseCorrelationId();

// Composes a patient + their recent history into one dashboard payload, both
// fetched through the Interceptor. DTOs are deserialized from CommonLib.
// Correlation IDs are automatically propagated via the middleware.
app.MapGet("/api/patients/{patientId}/dashboard", async (string patientId, HttpContext context, IHttpClientFactory httpClientFactory, ILogger<Program> logger) =>
{
    var correlationId = context.GetCorrelationId();
    var client = httpClientFactory.CreateClient("interceptor");

    try
    {
        // Add correlation ID to requests
        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            client.DefaultRequestHeaders.Add(CorrelationIdConstants.HeaderName, correlationId);
        }

        var patient = await client.GetFromJsonAsync<PatientDto>($"/api/patient-service/patients/{patientId}");
        var history = await client.GetFromJsonAsync<List<PatientHistoryEntryDto>>($"/api/patient-service/patients/{patientId}/history")
                      ?? new List<PatientHistoryEntryDto>();

        logger.LogInformation("Dashboard composition succeeded for {PatientId} with correlation ID {CorrelationId}", 
            patientId, correlationId);
        
        return Results.Ok(new PatientDashboardDto(patient!, history));
    }
    catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
    {
        logger.LogWarning("Patient {PatientId} not found", patientId);
        return Results.NotFound();
    }
    catch (HttpRequestException ex)
    {
        logger.LogError(ex, "Dashboard composition failed for {PatientId} with correlation ID {CorrelationId}", 
            patientId, correlationId);
        return Results.Problem("Could not reach patient-service through the Interceptor.", statusCode: StatusCodes.Status502BadGateway);
    }
});

app.Run();

public partial class Program { }
