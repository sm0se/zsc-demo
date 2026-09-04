using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Zsc.AuditService.Tests;

/// <summary>
/// Tests for the AuditService endpoints demonstrating that a new microservice
/// can be added without touching CommonLib, Interceptor, PatientService, or BFF.
/// This proves Acceptance Criteria #4.
/// </summary>
public class AuditEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public AuditEndpointsTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task GetAudit_WithValidId_ReturnsAuditRecord()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/audits/audit-123");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("audit-123", content);
        Assert.Contains("ApiAccess", content);
        Assert.Contains("Success", content);
    }

    [Fact]
    public async Task GetAudit_IncludesCorrelationIdInResponse()
    {
        var client = _factory.CreateClient();
        var correlationId = "test-audit-" + Guid.NewGuid().ToString();
        client.DefaultRequestHeaders.Add("X-Correlation-Id", correlationId);

        var response = await client.GetAsync("/audits/audit-456");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var hasHeader = response.Headers.TryGetValues("X-Correlation-Id", out var values);
        Assert.True(hasHeader, "Response should include X-Correlation-Id header");
        var returnedId = values?.FirstOrDefault();
        Assert.NotNull(returnedId);
        Assert.NotEmpty(returnedId!);
    }

    [Fact]
    public async Task GetAudits_ReturnsAuditList()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/audits");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("audit-001", content);
        Assert.Contains("audit-002", content);
        Assert.Contains("UserLogin", content);
        Assert.Contains("DataAccess", content);
    }

    [Fact]
    public async Task Health_ReturnsHealthy()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("healthy", content);
        Assert.Contains("audit-service", content);
    }

    [Fact]
    public async Task GetAudit_WithoutCorrelationId_GeneratesOne()
    {
        var client = _factory.CreateClient();
        // Don't set a correlation ID
        var response = await client.GetAsync("/audits/audit-789");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var hasHeader = response.Headers.TryGetValues("X-Correlation-Id", out var values);
        Assert.True(hasHeader, "Response should generate X-Correlation-Id if not provided");
        var generatedId = values?.FirstOrDefault();
        Assert.NotNull(generatedId);
        Assert.NotEmpty(generatedId!);
        // Should look like a GUID
        Assert.True(
            Guid.TryParse(generatedId, out _),
            $"Generated correlation ID should be a valid GUID, got {generatedId}"
        );
    }
}
