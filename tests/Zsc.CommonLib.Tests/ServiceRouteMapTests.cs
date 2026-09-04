using Microsoft.Extensions.Logging;
using Zsc.CommonLib.ServiceDiscovery;

namespace Zsc.CommonLib.Tests;

public class ServiceDiscoveryClientTests
{
    [Fact]
    public async Task Resolve_ServiceNotRegistered_ReturnsNull()
    {
        // This test demonstrates that the discovery client gracefully handles
        // unregistered services. In a real scenario, this would call the
        // ServiceDiscovery service; here we just verify the contract.
        var mockHttpClient = new HttpClient();
        var mockLogger = new MockLogger<HttpServiceDiscoveryClient>();
        var client = new HttpServiceDiscoveryClient(mockHttpClient, mockLogger);

        // When no service discovery server is running, GetAsync will fail,
        // and the client returns null rather than throwing.
        var result = await client.ResolveAsync("nonexistent-service");

        Assert.Null(result);
    }

    private class MockLogger<T> : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) { }
    }
}
