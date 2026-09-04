namespace Zsc.ServiceDiscovery;

public interface IInMemoryRegistry
{
    void Register(string serviceName, ServiceRegistration registration);
    ServiceRegistration? Resolve(string serviceName);
}
