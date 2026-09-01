using Zsc.CommonLib.Routing;

namespace Zsc.CommonLib.Http;

// Every caller that wants to reach another ZSC service goes through here:
// resolve the address out of ServiceRouteMap, then build a plain HttpClient
// pointed at it. No service identity, no correlation id - just an address.
public static class RoutedHttpClientFactory
{
    public static HttpClient CreateFor(string serviceName)
        => new() { BaseAddress = new Uri(ServiceRouteMap.Resolve(serviceName).HttpBaseUrl) };
}
