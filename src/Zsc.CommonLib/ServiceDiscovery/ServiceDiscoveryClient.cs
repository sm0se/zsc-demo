using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

namespace Zsc.CommonLib.ServiceDiscovery;

public sealed record ServiceEntry(
    string ServiceName,
    string? HttpBaseUrl,
    string? GrpcAddress,
    string? EventTopic);

public interface IServiceDiscoveryClient
{
    /// <summary>
    /// Resolve a service's addresses by name from the service discovery server.
    /// Throws KeyNotFoundException if the service is not registered.
    /// </summary>
    Task<ServiceEntry> ResolveAsync(string serviceName, CancellationToken cancellationToken = default);
}

public sealed class HttpServiceDiscoveryClient : IServiceDiscoveryClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HttpServiceDiscoveryClient> _logger;

    public HttpServiceDiscoveryClient(HttpClient httpClient, ILogger<HttpServiceDiscoveryClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ServiceEntry> ResolveAsync(string serviceName, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/services/{serviceName}/resolve", cancellationToken);
            
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Service '{ServiceName}' not found in discovery server", serviceName);
                throw new KeyNotFoundException($"Service '{serviceName}' is not registered in service discovery.");
            }

            response.EnsureSuccessStatusCode();
            
            var entry = await response.Content.ReadFromJsonAsync<ServiceEntry>(cancellationToken: cancellationToken);
            if (entry is null)
            {
                throw new InvalidOperationException($"Service discovery returned null response for '{serviceName}'");
            }
            
            _logger.LogDebug("Resolved service '{ServiceName}': HTTP={Http} gRPC={Grpc}",
                serviceName, entry.HttpBaseUrl ?? "(none)", entry.GrpcAddress ?? "(none)");
            
            return entry;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to resolve service '{ServiceName}' from discovery server", serviceName);
            throw;
        }
    }
}
