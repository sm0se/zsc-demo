using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using Zsc.CommonLib;

namespace Zsc.Bff.Tests;

/// <summary>
/// Tests proving that the BFF generates and propagates correlation IDs to the Interceptor.
/// </summary>
public class CorrelationIdTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public CorrelationIdTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task Request_WithoutCorrelationId_BFFGeneratesOne()
    {
        var client = _factory.CreateClient();

        // Make a request to BFF without a correlation ID
        var response = await client.GetAsync("/api/patients/test-id/dashboard");

        // Response should include a correlation ID header (even if the downstream call fails)
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
    public async Task Request_WithCorrelationId_BFFPropagatesIt()
    {
        var client = _factory.CreateClient();
        var testCorrelationId = "bff-test-correlation-id-99999";

        // Make a request with a correlation ID
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/patients/test-id/dashboard");
        request.Headers.Add(CorrelationIdConstants.HeaderName, testCorrelationId);

        var response = await client.SendAsync(request);

        // Response should echo the same correlation ID
        Assert.True(
            response.Headers.TryGetValues(CorrelationIdConstants.HeaderName, out var correlationIds),
            $"Response missing {CorrelationIdConstants.HeaderName} header");

        var returnedId = correlationIds.FirstOrDefault();
        Assert.Equal(testCorrelationId, returnedId);
    }
}
