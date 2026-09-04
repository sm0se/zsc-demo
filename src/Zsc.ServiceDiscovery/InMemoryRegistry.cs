namespace Zsc.ServiceDiscovery;

public class InMemoryRegistry : IInMemoryRegistry
{
    private readonly Dictionary<string, ServiceRegistration> _registry = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    public void Register(string serviceName, ServiceRegistration registration)
    {
        lock (_lock)
        {
            _registry[serviceName] = registration;
        }
    }

    public ServiceRegistration? Resolve(string serviceName)
    {
        lock (_lock)
        {
            _registry.TryGetValue(serviceName, out var registration);
            return registration;
        }
    }
}
