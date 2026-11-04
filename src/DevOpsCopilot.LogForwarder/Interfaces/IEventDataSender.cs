using Azure.Messaging.EventHubs;

namespace DevOpsCopilot.LogForwarder.Interfaces;

public interface IEventDataSender
{
	Task SendAsync(IReadOnlyCollection<EventData> events);
}
