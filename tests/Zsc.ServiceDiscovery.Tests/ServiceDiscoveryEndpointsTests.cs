using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Zsc.ServiceDiscovery.Tests;

/// <summary>
/// Tests for the ServiceDiscovery service register/resolve endpoints.
/// These tests verify that services can self-register and be discovered at runtime.
/// </summary>
public class ServiceDiscoveryEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ServiceDiscoveryEndpointsTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task Register_ValidService_ReturnsOk()
    {
        var client = _factory.CreateClient();
        var registration = new
        {
            httpBaseUrl = "http://localhost:5001",
            grpcAddress = "http://localhost:5002",
            eventTopic = "my-service-events"
        };

        var response = await client.PostAsJsonAsync("/services/test-service/register", registration);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("test-service", content);
        Assert.Contains("registered", content);
    }

    [Fact]
    public async Task Register_ServiceWithHttpOnly_ReturnsOk()
    {
        var client = _factory.CreateClient();
        var registration = new
        {
            httpBaseUrl = "http://localhost:5001",
            grpcAddress = (string?)null,
            eventTopic = (string?)null
        };

        var response = await client.PostAsJsonAsync("/services/http-only-service/register", registration);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Register_ServiceWithGrpcOnly_ReturnsOk()
    {
        var client = _factory.CreateClient();
        var registration = new
        {
            httpBaseUrl = (string?)null,
            grpcAddress = "http://localhost:5002",
            eventTopic = (string?)null
        };

        var response = await client.PostAsJsonAsync("/services/grpc-only-service/register", registration);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Register_NoAddresses_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();
        var registration = new
        {
            httpBaseUrl = (string?)null,
            grpcAddress = (string?)null,
            eventTopic = "some-topic"
        };

        var response = await client.PostAsJsonAsync("/services/invalid-service/register", registration);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RegisterThenResolve_KnownService_ReturnsRegisteredAddresses()
    {
        var client = _factory.CreateClient();
        var registration = new
        {
            httpBaseUrl = "http://localhost:9001",
            grpcAddress = "http://localhost:9002",
            eventTopic = "resolve-test-events"
        };

        // Register
        var registerResponse = await client.PostAsJsonAsync("/services/resolve-test-service/register", registration);
        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);

        // Resolve
        var resolveResponse = await client.GetAsync("/services/resolve-test-service/resolve");
        Assert.Equal(HttpStatusCode.OK, resolveResponse.StatusCode);

        var content = await resolveResponse.Content.ReadAsStringAsync();
        Assert.Contains("http://localhost:9001", content);
        Assert.Contains("http://localhost:9002", content);
        Assert.Contains("resolve-test-events", content);
    }

    [Fact]
    public async Task Resolve_UnregisteredService_Returns404()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/services/nonexistent-service/resolve");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("not found", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Register_MultipleServices_EachResolvesCorrectly()
    {
        var client = _factory.CreateClient();

        // Register service 1
        var reg1 = new { httpBaseUrl = "http://svc1:8001", grpcAddress = (string?)null, eventTopic = (string?)null };
        await client.PostAsJsonAsync("/services/service-1/register", reg1);

        // Register service 2
        var reg2 = new { httpBaseUrl = "http://svc2:8002", grpcAddress = "http://svc2:9002", eventTopic = "svc2-events" };
        await client.PostAsJsonAsync("/services/service-2/register", reg2);

        // Resolve service 1
        var res1 = await client.GetAsync("/services/service-1/resolve");
        Assert.Equal(HttpStatusCode.OK, res1.StatusCode);
        var content1 = await res1.Content.ReadAsStringAsync();
        Assert.Contains("http://svc1:8001", content1);

        // Resolve service 2
        var res2 = await client.GetAsync("/services/service-2/resolve");
        Assert.Equal(HttpStatusCode.OK, res2.StatusCode);
        var content2 = await res2.Content.ReadAsStringAsync();
        Assert.Contains("http://svc2:8002", content2);
        Assert.Contains("http://svc2:9002", content2);
    }

    [Fact]
    public async Task Register_ServiceTwice_OverwritesFirstRegistration()
    {
        var client = _factory.CreateClient();

        // Register with version 1
        var reg1 = new { httpBaseUrl = "http://localhost:6001", grpcAddress = (string?)null, eventTopic = (string?)null };
        await client.PostAsJsonAsync("/services/update-test/register", reg1);

        // Resolve to verify
        var res1 = await client.GetAsync("/services/update-test/resolve");
        var content1 = await res1.Content.ReadAsStringAsync();
        Assert.Contains("http://localhost:6001", content1);

        // Register with version 2 (updated address)
        var reg2 = new { httpBaseUrl = "http://localhost:6002", grpcAddress = (string?)null, eventTopic = (string?)null };
        await client.PostAsJsonAsync("/services/update-test/register", reg2);

        // Resolve to verify update
        var res2 = await client.GetAsync("/services/update-test/resolve");
        var content2 = await res2.Content.ReadAsStringAsync();
        Assert.Contains("http://localhost:6002", content2);
        Assert.DoesNotContain("http://localhost:6001", content2);
    }

    [Fact]
    public async Task Resolve_CaseInsensitive_FindsService()
    {
        var client = _factory.CreateClient();

        // Register with lowercase
        var registration = new { httpBaseUrl = "http://localhost:7001", grpcAddress = (string?)null, eventTopic = (string?)null };
        await client.PostAsJsonAsync("/services/case-test/register", registration);

        // Resolve with uppercase
        var response = await client.GetAsync("/services/CASE-TEST/resolve");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("http://localhost:7001", content);
    }
}
