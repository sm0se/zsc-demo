namespace Zsc.CommonLib.ServiceDiscovery;

public sealed record ServiceInfo(
    string? HttpBaseUrl = null,
    string? GrpcAddress = null,
    string? EventTopic = null);

public interface IServiceDiscoveryClient
{
    Task<ServiceInfo?> ResolveAsync(string serviceName, CancellationToken cancellationToken = default);
}
