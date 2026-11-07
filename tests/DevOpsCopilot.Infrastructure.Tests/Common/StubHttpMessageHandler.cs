using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace DevOpsCopilot.Infrastructure.Tests.Common;

/// <summary>
/// Answers each request with the next queued status and body (202 with an empty body when the
/// queue is empty) and records every request it saw, so a notifier test can assert the exact
/// call without a network — the way <c>FakeChatClient</c> covers Azure OpenAI.
/// </summary>
internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<(HttpStatusCode Status, string Body, int? RetryAfterSeconds)> _responses = new();

    public List<RecordedRequest> Requests { get; } = [];

    public void Respond(HttpStatusCode status, string body = "", int? retryAfterSeconds = null) =>
        _responses.Enqueue((status, body, retryAfterSeconds));

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // A real handler throws before sending when the token is already cancelled.
        cancellationToken.ThrowIfCancellationRequested();
        var body = request.Content is null
            ? ""
            : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add(new RecordedRequest(
            request.Method,
            request.RequestUri,
            body,
            request.Content?.Headers.ContentType?.MediaType,
            request.Headers.Authorization?.ToString()));

        var (status, responseBody, retryAfter) = _responses.Count > 0
            ? _responses.Dequeue()
            : (HttpStatusCode.Accepted, "", null);
        var response = new HttpResponseMessage(status)
        {
            Content = new StringContent(responseBody, Encoding.UTF8, "application/json"),
            RequestMessage = request,
        };
        if (retryAfter is not null)
        {
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(retryAfter.Value));
        }

        return response;
    }
}
