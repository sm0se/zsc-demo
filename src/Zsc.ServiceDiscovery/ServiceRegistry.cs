namespace Zsc.ServiceDiscovery;

public sealed record ServiceEntry(
    string ServiceName,
    string? HttpBaseUrl,
    string? GrpcAddress,
    string? EventTopic)
{
    public ServiceEntry Validate()
    {
        if (string.IsNullOrWhiteSpace(HttpBaseUrl) && string.IsNullOrWhiteSpace(GrpcAddress))
        {
            throw new ArgumentException(
                $"Service '{ServiceName}' must have at least one of HttpBaseUrl or GrpcAddress.");
        }
        return this;
    }
}

public interface IServiceRegistry
{
    void Register(ServiceEntry entry);
    ServiceEntry? Resolve(string serviceName);
}

public sealed class InMemoryServiceRegistry : IServiceRegistry
{
    private readonly Dictionary<string, ServiceEntry> _registry = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    public void Register(ServiceEntry entry)
    {
        entry.Validate();
        lock (_lock)
        {
            _registry[entry.ServiceName] = entry;
        }
    }

    public ServiceEntry? Resolve(string serviceName)
    {
        lock (_lock)
        {
            _registry.TryGetValue(serviceName, out var entry);
            return entry;
        }
    }
}
