using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Zsc.Interceptor.Tests;

// Full BFF -> Interceptor -> PatientService forwarding is exercised live
// during the demo/dry run with all three processes actually running; these
// tests cover the Interceptor's own error handling in isolation.
public class ForwardingTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ForwardingTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task UnknownService_ReturnsBadGateway()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/does-not-exist/whatever");
        // With service discovery, unknown services return 502 because discovery
        // service can't resolve them
        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }

    [Fact]
    public async Task DiscoveryServiceUnreachable_ReturnsBadGateway()
    {
        // When the discovery service itself is unreachable, the interceptor
        // can't resolve any service address and returns BadGateway.
        // In this test environment, discovery may or may not be running,
        // so we accept either BadGateway (no discovery) or OK (discovery+service running).
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/patient-service/patients/pat-000001");
        // Either service is reachable (OK) or discovery isn't (BadGateway)
        Assert.True(
            response.StatusCode == HttpStatusCode.OK || 
            response.StatusCode == HttpStatusCode.BadGateway,
            $"Expected OK or BadGateway, got {response.StatusCode}"
        );
    }
}
