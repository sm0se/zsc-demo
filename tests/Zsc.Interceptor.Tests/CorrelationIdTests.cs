using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using Zsc.CommonLib;

namespace Zsc.Interceptor.Tests;

/// <summary>
/// Tests proving that the Interceptor generates and propagates correlation IDs.
/// </summary>
public class CorrelationIdTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public CorrelationIdTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task Request_WithoutCorrelationId_InterceptorGeneratesOne()
    {
        var client = _factory.CreateClient();

        // Make a request WITHOUT providing a correlation ID
        var response = await client.GetAsync("/api/patient-service/patients/test-id");

        // Response should include a correlation ID header (even if request fails downstream)
        Assert.True(
            response.Headers.TryGetValues(CorrelationIdConstants.HeaderName, out var correlationIds),
            $"Response missing {CorrelationIdConstants.HeaderName} header");

        var correlationId = correlationIds.FirstOrDefault();
        Assert.NotNull(correlationId);
        Assert.NotEmpty(correlationId);
        
        // Should be a valid GUID
        Assert.True(Guid.TryParse(correlationId, out _), $"Correlation ID '{correlationId}' is not a valid GUID");
    }

    [Fact]
    public async Task Request_WithCorrelationId_InterceptorPropagatesIt()
    {
        var client = _factory.CreateClient();
        var testCorrelationId = "test-correlation-id-12345";

        // Make a request WITH a correlation ID header
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/patient-service/patients/test-id");
        request.Headers.Add(CorrelationIdConstants.HeaderName, testCorrelationId);

        var response = await client.SendAsync(request);

        // Response should echo the same correlation ID
        Assert.True(
            response.Headers.TryGetValues(CorrelationIdConstants.HeaderName, out var correlationIds),
            $"Response missing {CorrelationIdConstants.HeaderName} header");

        var returnedId = correlationIds.FirstOrDefault();
        Assert.Equal(testCorrelationId, returnedId);
    }

    [Fact]
    public async Task BadGateway_Response_StillIncludesCorrelationId()
    {
        var client = _factory.CreateClient();
        var testCorrelationId = "bad-gateway-test-id";

        // Make a request to a non-existent service
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/does-not-exist/whatever");
        request.Headers.Add(CorrelationIdConstants.HeaderName, testCorrelationId);

        var response = await client.SendAsync(request);

        // Should get 502 Bad Gateway
        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);

        // But correlation ID should still be in response
        Assert.True(
            response.Headers.TryGetValues(CorrelationIdConstants.HeaderName, out var correlationIds),
            "Bad gateway response missing correlation ID header");

        Assert.Equal(testCorrelationId, correlationIds.FirstOrDefault());
    }

    [Fact]
    public async Task MultipleRequests_EachGetsUniqueCorrelationId()
    {
        var client = _factory.CreateClient();
        var correlationIds = new List<string>();

        // Make multiple requests without specifying correlation IDs
        for (int i = 0; i < 3; i++)
        {
            var response = await client.GetAsync("/api/patient-service/patients/test-id");
            
            if (response.Headers.TryGetValues(CorrelationIdConstants.HeaderName, out var ids))
            {
                correlationIds.Add(ids.FirstOrDefault() ?? "");
            }
        }

        // Should have 3 correlation IDs
        Assert.Equal(3, correlationIds.Count);

        // All should be non-empty
        Assert.All(correlationIds, id => Assert.NotEmpty(id));

        // All should be unique (or at least the first and last should differ)
        Assert.NotEqual(correlationIds[0], correlationIds[1]);
        Assert.NotEqual(correlationIds[1], correlationIds[2]);
    }
}
