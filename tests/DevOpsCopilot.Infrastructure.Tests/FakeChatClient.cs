using Microsoft.Extensions.AI;

namespace DevOpsCopilot.Infrastructure.Tests;

/// <summary>
/// A chat client that answers with whatever text the test gives it and records what it was asked.
/// Structured output is parsed from the response text, so returning JSON here is enough to drive
/// the diagnosis model without a network call.
/// </summary>
internal sealed class FakeChatClient : IChatClient
{
    private readonly Func<IEnumerable<ChatMessage>, CancellationToken, Task<string>> _answer;

    public FakeChatClient(string responseText)
        : this((_, _) => Task.FromResult(responseText))
    {
    }

    public FakeChatClient(Func<IEnumerable<ChatMessage>, CancellationToken, Task<string>> answer)
    {
        _answer = answer;
    }

    public List<ChatMessage> Received { get; } = [];

    public int Calls { get; private set; }

    public string Prompt => string.Join("\n", Received.Select(message => message.Text));

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        Calls++;
        Received.AddRange(messages);
        var text = await _answer(messages, cancellationToken);
        return new ChatResponse(new ChatMessage(ChatRole.Assistant, text));
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
    }
}
