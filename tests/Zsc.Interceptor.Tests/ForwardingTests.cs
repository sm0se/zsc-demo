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
        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }

    [Fact]
    public async Task KnownService_UpstreamUnreachable_ReturnsBadGateway()
    {
        // patient-service isn't running in this test, so the route resolves
        // but the forward call itself fails - this is the "no downstream
        // reachable" path rather than the "no route" path above.
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/patient-service/patients/pat-000001");
        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }
}
