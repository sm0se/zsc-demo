using Zsc.CommonLib.Routing;

namespace Zsc.CommonLib.Http;

/// <summary>
/// DEPRECATED: Services should now resolve addresses via IServiceDiscoveryClient instead.
/// </summary>
[Obsolete("Use IServiceDiscoveryClient for runtime routing.", false)]
public static class RoutedHttpClientFactory
{
    public static HttpClient CreateFor(string serviceName)
        => new() { BaseAddress = new Uri(ServiceRouteMap.Resolve(serviceName).HttpBaseUrl!) };
}
