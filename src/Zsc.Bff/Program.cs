using System.Net;
using Zsc.CommonLib.Dtos;
using Zsc.CommonLib.ServiceDiscovery;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient("interceptor", client =>
{
    var baseUrl = builder.Configuration["Interceptor:BaseUrl"] ?? "http://localhost:5200";
    client.BaseAddress = new Uri(baseUrl);
});

var discoveryBaseUrl = builder.Configuration["ServiceDiscovery:BaseUrl"] ?? "http://localhost:5300";
builder.Services.AddHttpClient<IServiceDiscoveryClient, HttpServiceDiscoveryClient>(client =>
{
    client.BaseAddress = new Uri(discoveryBaseUrl);
});

var app = builder.Build();

// Composes a patient + their recent history into one dashboard payload,
// both fetched through the Interceptor. Includes correlation-id propagation.
app.MapGet("/api/patients/{patientId}/dashboard", async (string patientId, IHttpClientFactory httpClientFactory, HttpContext context, ILogger<Program> logger) =>
{
    var correlationId = context.Request.Headers.ContainsKey("X-Correlation-Id")
        ? context.Request.Headers["X-Correlation-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString()
        : Guid.NewGuid().ToString();

    logger.LogInformation("Dashboard request for patient {PatientId} [CorrelationId={CorrelationId}]", patientId, correlationId);

    var client = httpClientFactory.CreateClient("interceptor");
    client.DefaultRequestHeaders.Add("X-Correlation-Id", correlationId);

    try
    {
        var patient = await client.GetFromJsonAsync<PatientDto>($"/api/patient-service/patients/{patientId}");
        var history = await client.GetFromJsonAsync<List<PatientHistoryEntryDto>>($"/api/patient-service/patients/{patientId}/history")
                      ?? new List<PatientHistoryEntryDto>();

        return Results.Ok(new PatientDashboardDto(patient!, history));
    }
    catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
    {
        logger.LogWarning("Patient not found: {PatientId} [CorrelationId={CorrelationId}]", patientId, correlationId);
        return Results.NotFound();
    }
    catch (HttpRequestException ex)
    {
        logger.LogError(ex, "Dashboard composition failed for {PatientId} [CorrelationId={CorrelationId}]", patientId, correlationId);
        return Results.Problem("Could not reach patient-service through the Interceptor.", statusCode: StatusCodes.Status502BadGateway);
    }
});

app.Run();

public partial class Program { }
