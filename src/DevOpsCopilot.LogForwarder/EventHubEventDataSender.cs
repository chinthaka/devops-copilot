using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Producer;

namespace DevOpsCopilot.LogForwarder;

public sealed class EventHubEventDataSender : IEventDataSender
{
	private readonly EventHubProducerClient _producer;

	public EventHubEventDataSender(EventHubProducerClient producer)
	{
		_producer = producer;
	}

	public Task SendAsync(IReadOnlyCollection<EventData> events) => _producer.SendAsync(events);
}
