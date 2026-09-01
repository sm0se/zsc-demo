using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Zsc.Bff.Tests;

public class PatientDashboardTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public PatientDashboardTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task Dashboard_InterceptorUnreachable_ReturnsBadGateway()
    {
        // Nothing is listening on the configured Interceptor base URL in this
        // test host, so the BFF's own error handling is what's under test.
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/patients/pat-000001/dashboard");
        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }
}
