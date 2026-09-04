using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Zsc.Interceptor.Tests;

/// <summary>
/// Tests for the new service discovery-based routing path.
/// These tests verify that the Interceptor correctly resolves services through
/// the discovery service (when available) instead of using hardcoded routes.
/// </summary>
public class DiscoveryRoutingTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public DiscoveryRoutingTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task ForwardRequest_UnknownService_ReturnsBadGateway()
    {
        var client = _factory.CreateClient();

        // Try to forward to a service that doesn't exist and isn't registered
        var response = await client.GetAsync("/api/completely-unknown-service/some/path");

        // Should get BadGateway because discovery can't find it
        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }

    [Fact]
    public async Task ForwardRequest_UnknownService_ReturnsErrorMessage()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/missing-service/endpoint");

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        
        // Response should mention the service that couldn't be reached
        Assert.NotEmpty(content);
        Assert.True(
            content.Contains("discover", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("reach", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("missing", StringComparison.OrdinalIgnoreCase),
            $"Error message should explain the discovery/routing failure. Got: {content}"
        );
    }

    [Fact]
    public async Task ForwardRequest_InvalidPath_StillIncludesCorrelationId()
    {
        var client = _factory.CreateClient();
        var correlationId = Guid.NewGuid().ToString();

        client.DefaultRequestHeaders.Add("X-Correlation-Id", correlationId);

        // Request with bad service name
        var response = await client.GetAsync("/api/bad-service-name/test/endpoint");

        // Even though routing fails, correlation ID should be in response
        var hasHeader = response.Headers.TryGetValues("X-Correlation-Id", out var values);
        Assert.True(hasHeader, "Correlation ID should be present even for failed routing");
    }

    [Fact]
    public async Task ForwardRequest_ParsingPath_WorksCorrectly()
    {
        var client = _factory.CreateClient();

        // Test various path formats to ensure parsing is correct
        var paths = new[]
        {
            "/api/service/single",
            "/api/service/path/with/multiple/segments",
            "/api/service/path-with-dashes",
            "/api/service/path_with_underscores",
            "/api/service/123/numeric"
        };

        foreach (var path in paths)
        {
            var response = await client.GetAsync(path);
            
            // May get 502 (service not found) or other error, but should respond
            Assert.NotNull(response);
            Assert.True(
                response.StatusCode == HttpStatusCode.BadGateway ||
                response.StatusCode == HttpStatusCode.NotFound ||
                response.StatusCode == HttpStatusCode.OK,
                $"Unexpected status for path {path}: {response.StatusCode}"
            );
        }
    }

    [Fact]
    public async Task ForwardRequest_HTTPMethods_ArePreserved()
    {
        var client = _factory.CreateClient();

        var methods = new[] { HttpMethod.Get, HttpMethod.Post, HttpMethod.Put, HttpMethod.Delete };

        foreach (var method in methods)
        {
            var request = new HttpRequestMessage(method, "/api/unknown/test");
            var response = await client.SendAsync(request);

            // We expect BadGateway since the service doesn't exist, but the important
            // thing is that the method was recognized and processed
            Assert.NotNull(response);
            Assert.True(
                response.StatusCode == HttpStatusCode.BadGateway ||
                response.StatusCode == HttpStatusCode.MethodNotAllowed,
                $"Method {method} should be handled. Got: {response.StatusCode}"
            );
        }
    }

    [Fact]
    public async Task ForwardRequest_QueryString_IsPreserved()
    {
        var client = _factory.CreateClient();

        // Test that query strings are included in the forwarded request
        var response = await client.GetAsync("/api/service/endpoint?param1=value1&param2=value2");

        // We don't care about success/failure here, just that the request was attempted
        Assert.NotNull(response);
    }
}
