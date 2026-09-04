using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Zsc.ServiceDiscovery.Tests;

/// <summary>
/// Tests for the ServiceDiscovery registry endpoints:
/// POST /services/{name}/register and GET /services/{name}/resolve
/// </summary>
public class RegistryTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public RegistryTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Register_ValidService_Returns200()
    {
        var client = _factory.CreateClient();
        var registerRequest = new { httpBaseUrl = "http://localhost:5101", grpcAddress = "http://localhost:5102" };

        var response = await client.PostAsJsonAsync("/services/test-service/register", registerRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Register_ThenResolve_ReturnsRegisteredService()
    {
        var client = _factory.CreateClient();
        var registerRequest = new 
        { 
            httpBaseUrl = "http://localhost:6001",
            grpcAddress = "http://localhost:6002",
            eventTopic = "test-events"
        };

        // Register the service
        var registerResponse = await client.PostAsJsonAsync("/services/audit-service/register", registerRequest);
        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);

        // Resolve it back
        var resolveResponse = await client.GetAsync("/services/audit-service/resolve");
        Assert.Equal(HttpStatusCode.OK, resolveResponse.StatusCode);

        var content = await resolveResponse.Content.ReadFromJsonAsync<ServiceEntryDto>();
        Assert.NotNull(content);
        Assert.Equal("audit-service", content.ServiceName);
        Assert.Equal("http://localhost:6001", content.HttpBaseUrl);
        Assert.Equal("http://localhost:6002", content.GrpcAddress);
        Assert.Equal("test-events", content.EventTopic);
    }

    [Fact]
    public async Task Resolve_UnregisteredService_Returns404()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/services/nonexistent-service/resolve");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Register_OnlyHttpUrl_Returns200()
    {
        var client = _factory.CreateClient();
        var registerRequest = new { httpBaseUrl = "http://localhost:7001" };

        var response = await client.PostAsJsonAsync("/services/http-only-service/register", registerRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Verify it was registered
        var resolveResponse = await client.GetAsync("/services/http-only-service/resolve");
        Assert.Equal(HttpStatusCode.OK, resolveResponse.StatusCode);
    }

    [Fact]
    public async Task Register_OnlyGrpcAddress_Returns200()
    {
        var client = _factory.CreateClient();
        var registerRequest = new { grpcAddress = "http://localhost:7102" };

        var response = await client.PostAsJsonAsync("/services/grpc-only-service/register", registerRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Verify it was registered
        var resolveResponse = await client.GetAsync("/services/grpc-only-service/resolve");
        Assert.Equal(HttpStatusCode.OK, resolveResponse.StatusCode);
    }

    [Fact]
    public async Task Register_NoHttpOrGrpc_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();
        var registerRequest = new { eventTopic = "only-events" };

        var response = await client.PostAsJsonAsync("/services/invalid-service/register", registerRequest);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_MultipleServices_EachResolvable()
    {
        var client = _factory.CreateClient();

        // Register service 1
        var register1 = new { httpBaseUrl = "http://localhost:8001" };
        var response1 = await client.PostAsJsonAsync("/services/service-1/register", register1);
        Assert.Equal(HttpStatusCode.OK, response1.StatusCode);

        // Register service 2
        var register2 = new { httpBaseUrl = "http://localhost:8002" };
        var response2 = await client.PostAsJsonAsync("/services/service-2/register", register2);
        Assert.Equal(HttpStatusCode.OK, response2.StatusCode);

        // Both should be resolvable
        var resolve1 = await client.GetAsync("/services/service-1/resolve");
        Assert.Equal(HttpStatusCode.OK, resolve1.StatusCode);

        var resolve2 = await client.GetAsync("/services/service-2/resolve");
        Assert.Equal(HttpStatusCode.OK, resolve2.StatusCode);

        // Verify correct addresses
        var content1 = await resolve1.Content.ReadFromJsonAsync<ServiceEntryDto>();
        Assert.Equal("http://localhost:8001", content1.HttpBaseUrl);

        var content2 = await resolve2.Content.ReadFromJsonAsync<ServiceEntryDto>();
        Assert.Equal("http://localhost:8002", content2.HttpBaseUrl);
    }

    [Fact]
    public async Task Register_OverwriteExisting_UpdatesAddress()
    {
        var client = _factory.CreateClient();

        // Register with first address
        var register1 = new { httpBaseUrl = "http://localhost:9001" };
        await client.PostAsJsonAsync("/services/update-test/register", register1);

        var resolve1 = await client.GetAsync("/services/update-test/resolve");
        var content1 = await resolve1.Content.ReadFromJsonAsync<ServiceEntryDto>();
        Assert.Equal("http://localhost:9001", content1.HttpBaseUrl);

        // Re-register with new address
        var register2 = new { httpBaseUrl = "http://localhost:9002" };
        await client.PostAsJsonAsync("/services/update-test/register", register2);

        var resolve2 = await client.GetAsync("/services/update-test/resolve");
        var content2 = await resolve2.Content.ReadFromJsonAsync<ServiceEntryDto>();
        Assert.Equal("http://localhost:9002", content2.HttpBaseUrl);
    }
}

// DTO for deserializing response
public sealed class ServiceEntryDto
{
    public string? ServiceName { get; set; }
    public string? HttpBaseUrl { get; set; }
    public string? GrpcAddress { get; set; }
    public string? EventTopic { get; set; }
}
