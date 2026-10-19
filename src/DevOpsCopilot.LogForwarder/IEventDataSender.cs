using Azure.Messaging.EventHubs;

namespace DevOpsCopilot.LogForwarder;

public interface IEventDataSender
{
	Task SendAsync(IReadOnlyCollection<EventData> events);
}
