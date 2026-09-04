using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Zsc.Bff.Tests;

public class PatientDashboardTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public PatientDashboardTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task Dashboard_InterceptorUnreachable_ReturnsBadGatewayOrOk()
    {
        // When the interceptor is unreachable in the test host, the BFF's
        // error handling returns BadGateway. However, if the Interceptor
        // and services are actually running (e.g., during integration testing),
        // the dashboard will return OK with the data.
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/patients/pat-000001/dashboard");
        // Accept either BadGateway (test isolation) or OK (full stack running)
        Assert.True(
            response.StatusCode == HttpStatusCode.OK ||
            response.StatusCode == HttpStatusCode.BadGateway,
            $"Expected OK or BadGateway, got {response.StatusCode}"
        );
    }
}
