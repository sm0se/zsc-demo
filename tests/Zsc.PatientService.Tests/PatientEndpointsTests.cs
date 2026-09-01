using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Zsc.CommonLib.Dtos;

namespace Zsc.PatientService.Tests;

public class PatientEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public PatientEndpointsTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task CreateThenGet_RoundTrips()
    {
        var client = _factory.CreateClient();

        var createResponse = await client.PostAsJsonAsync(
            "/patients",
            new CreatePatientRequest("MRN-99999", "Test Patient", new DateOnly(1990, 1, 1)));
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<PatientDto>();
        Assert.NotNull(created);

        var getResponse = await client.GetAsync($"/patients/{created!.PatientId}");
        getResponse.EnsureSuccessStatusCode();
        var fetched = await getResponse.Content.ReadFromJsonAsync<PatientDto>();

        Assert.Equal(created.PatientId, fetched!.PatientId);
        Assert.Equal("Test Patient", fetched.DisplayName);
    }

    [Fact]
    public async Task Get_UnknownPatient_ReturnsNotFound()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/patients/does-not-exist");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AddHistory_ThenGetHistory_ReturnsEntry()
    {
        var client = _factory.CreateClient();

        var createResponse = await client.PostAsJsonAsync(
            "/patients",
            new CreatePatientRequest("MRN-88888", "History Patient", new DateOnly(1992, 6, 15)));
        var created = await createResponse.Content.ReadFromJsonAsync<PatientDto>();

        var addHistoryResponse = await client.PostAsJsonAsync(
            $"/patients/{created!.PatientId}/history",
            new PatientHistoryEntryDto(created.PatientId, DateTimeOffset.UtcNow, "Pre-op assessment completed"));
        addHistoryResponse.EnsureSuccessStatusCode();

        var historyResponse = await client.GetAsync($"/patients/{created.PatientId}/history");
        var history = await historyResponse.Content.ReadFromJsonAsync<List<PatientHistoryEntryDto>>();

        Assert.Contains(history!, h => h.Description == "Pre-op assessment completed");
    }
}
