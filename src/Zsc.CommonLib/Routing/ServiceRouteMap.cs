namespace Zsc.CommonLib.Routing;

// The routing table every ZSC service links against to find every other
// service. This is the "common library dependency" Requirement #2 wants
// removed: addresses live here, in code, shared by reference - not looked
// up from anything that can change at runtime. Registering a new
// microservice means adding an entry here AND redeploying every service
// that already depends on this library, AND wiring a forwarding rule into
// Zsc.Interceptor, AND (if the BFF needs it) a new BFF endpoint. One new
// API today touches four places.
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
                $"No route registered for service '{serviceName}'. Add it to " +
                $"{nameof(ServiceRouteMap)} and redeploy every service that references Zsc.CommonLib.");
        }

        return entry;
    }
}
