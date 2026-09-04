using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

namespace Zsc.CommonLib.ServiceDiscovery;

public class HttpServiceDiscoveryClient : IServiceDiscoveryClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HttpServiceDiscoveryClient> _logger;

    public HttpServiceDiscoveryClient(HttpClient httpClient, ILogger<HttpServiceDiscoveryClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<ServiceInfo?> ResolveAsync(string serviceName, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/services/{serviceName}/resolve", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Service discovery returned {StatusCode} for service '{ServiceName}'.",
                    response.StatusCode, serviceName);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<ServiceInfo>(cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resolve service '{ServiceName}' from discovery service.", serviceName);
            return null;
        }
    }
}
