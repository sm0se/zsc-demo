using Zsc.CommonLib.Routing;

namespace Zsc.CommonLib.Tests;

public class ServiceRouteMapTests
{
    [Fact]
    public void Resolve_KnownService_ReturnsConfiguredAddresses()
    {
        var entry = ServiceRouteMap.Resolve("patient-service");

        Assert.Equal("patient-service", entry.ServiceName);
        Assert.Equal("http://localhost:5101", entry.HttpBaseUrl);
        Assert.Equal("http://localhost:5102", entry.GrpcAddress);
    }

    [Fact]
    public void Resolve_UnknownService_Throws()
    {
        Assert.Throws<KeyNotFoundException>(() => ServiceRouteMap.Resolve("does-not-exist"));
    }

    [Fact]
    public void Resolve_IsCaseInsensitive()
    {
        var entry = ServiceRouteMap.Resolve("PATIENT-SERVICE");
        Assert.Equal("patient-service", entry.ServiceName);
    }
}
