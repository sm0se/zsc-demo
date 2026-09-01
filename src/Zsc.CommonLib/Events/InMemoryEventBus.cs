using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Zsc.CommonLib.Events;

public sealed class InMemoryEventBus : IEventBus
{
    private readonly ILogger<InMemoryEventBus> _logger;

    public InMemoryEventBus(ILogger<InMemoryEventBus> logger) => _logger = logger;

    public Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default) where TEvent : class
    {
        _logger.LogInformation(
            "[event-bus] published {EventType}: {Payload}",
            typeof(TEvent).Name,
            JsonSerializer.Serialize(@event));
        return Task.CompletedTask;
    }
}
