namespace Zsc.CommonLib.Events;

// Stands in for a real service bus (Azure Service Bus / RabbitMQ / Kafka).
// Publishers depend on this interface only, so swapping InMemoryEventBus
// for a real transport later doesn't touch any publisher.
public interface IEventBus
{
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default) where TEvent : class;
}
