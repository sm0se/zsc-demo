namespace Zsc.CommonLib.Routing;

// One microservice's known address, keyed by protocol. Every service that
// wants to reach another one resolves it out of ServiceRouteMap and builds
// a client from whichever address it needs.
public sealed record ServiceRouteEntry(string ServiceName, string HttpBaseUrl, string? GrpcAddress = null);
