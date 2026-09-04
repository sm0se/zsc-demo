namespace Zsc.ServiceDiscovery;

public sealed record ServiceRegistration(
    string? HttpBaseUrl = null,
    string? GrpcAddress = null,
    string? EventTopic = null);
