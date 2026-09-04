namespace Zsc.CommonLib.Routing;

/// <summary>
/// DEPRECATED: Kept only as a startup seed for local development bootstrapping.
/// Production services should register with ServiceDiscovery on startup.
/// This class is no longer used for runtime routing - use IServiceDiscoveryClient instead.
/// 
/// In a distributed ZSC deployment, the ServiceDiscovery server (Zsc.ServiceDiscovery)
/// maintains the authoritative registry, and services resolve addresses at runtime
/// by calling the discovery endpoint. This enables adding new services without
/// modifying or redeploying existing code.
/// </summary>
[Obsolete("Use IServiceDiscoveryClient for runtime routing. ServiceRouteMap is kept only for dev bootstrap.", false)]
public static class ServiceRouteMap
{
    private static readonly IReadOnlyDictionary<string, ServiceRouteEntry> Routes =
        new Dictionary<string, ServiceRouteEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["patient-service"] = new ServiceRouteEntry(
                ServiceName: "patient-service",
                HttpBaseUrl: "http://localhost:5101",
                GrpcAddress: "http://localhost:5102"),
        };

    public static ServiceRouteEntry Resolve(string serviceName)
    {
        if (!Routes.TryGetValue(serviceName, out var entry))
        {
            throw new KeyNotFoundException(
                $"No route registered for service '{serviceName}'.");
        }

        return entry;
    }
}
