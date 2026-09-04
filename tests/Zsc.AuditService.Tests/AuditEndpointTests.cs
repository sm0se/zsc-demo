using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using Zsc.CommonLib;

namespace Zsc.AuditService.Tests;

/// <summary>
/// Tests for the AuditService endpoints and correlation ID propagation.
/// Proves that a new service can be added without modifying Interceptor, BFF, or PatientService.
/// </summary>
public class AuditEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public AuditEndpointTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task GetAudit_ReturnsAuditRecord()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/audits/audit-001");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var audit = await response.Content.ReadFromJsonAsync<AuditRecordDto>();
        Assert.NotNull(audit);
        Assert.Equal("audit-001", audit.auditId);
        Assert.Equal("VIEWED", audit.action);
        Assert.Equal("document", audit.resource);
    }

    [Fact]
    public async Task GetAudit_WithCorrelationId_PreservesIt()
    {
        var client = _factory.CreateClient();
        var testCorrelationId = "test-audit-correlation-123";

        var request = new HttpRequestMessage(HttpMethod.Get, "/audits/audit-002");
        request.Headers.Add(CorrelationIdConstants.HeaderName, testCorrelationId);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(
            response.Headers.TryGetValues(CorrelationIdConstants.HeaderName, out var correlationIds),
            "Response missing correlation ID header");

        var returnedId = correlationIds.FirstOrDefault();
        Assert.Equal(testCorrelationId, returnedId);
    }

    [Fact]
    public async Task GetAudit_GeneratesCorrelationIdIfMissing()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/audits/audit-003");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(
            response.Headers.TryGetValues(CorrelationIdConstants.HeaderName, out var correlationIds),
            "Response missing correlation ID header");

        var correlationId = correlationIds.FirstOrDefault();
        Assert.NotNull(correlationId);
        Assert.NotEmpty(correlationId);
        Assert.True(Guid.TryParse(correlationId, out _), "Correlation ID is not a valid GUID");
    }
}

public sealed class AuditRecordDto
{
    public string? auditId { get; set; }
    public DateTimeOffset timestamp { get; set; }
    public string? action { get; set; }
    public string? resource { get; set; }
    public string? details { get; set; }
}
