using System.Net;
using Zsc.CommonLib.Dtos;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient("interceptor", client =>
{
    var baseUrl = builder.Configuration["Interceptor:BaseUrl"] ?? "http://localhost:5200";
    client.BaseAddress = new Uri(baseUrl);
});

var app = builder.Build();

// Composes a patient + their recent history into one dashboard payload, both
// fetched through the Interceptor. This deserializes Zsc.CommonLib's own
// PatientDto/PatientHistoryEntryDto straight off the wire - a compile-time
// dependency on the shared library, not just a runtime one. Adding a new
// field here today means changing CommonLib's DTO, PatientService's
// response, and this endpoint, all at once.
app.MapGet("/api/patients/{patientId}/dashboard", async (string patientId, IHttpClientFactory httpClientFactory, ILogger<Program> logger) =>
{
    var client = httpClientFactory.CreateClient("interceptor");

    try
    {
        var patient = await client.GetFromJsonAsync<PatientDto>($"/api/patient-service/patients/{patientId}");
        var history = await client.GetFromJsonAsync<List<PatientHistoryEntryDto>>($"/api/patient-service/patients/{patientId}/history")
                      ?? new List<PatientHistoryEntryDto>();

        return Results.Ok(new PatientDashboardDto(patient!, history));
    }
    catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
    {
        return Results.NotFound();
    }
    catch (HttpRequestException ex)
    {
        logger.LogError(ex, "Dashboard composition failed for {PatientId}", patientId);
        return Results.Problem("Could not reach patient-service through the Interceptor.", statusCode: StatusCodes.Status502BadGateway);
    }
});

app.Run();

public partial class Program { }
