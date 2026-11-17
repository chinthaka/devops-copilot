namespace DevOpsCopilot.Infrastructure.Tests.Common;

/// <summary>One request seen by <see cref="StubHttpMessageHandler"/>.</summary>
internal sealed record RecordedRequest(
    HttpMethod Method,
    Uri? Uri,
    string Body,
    string? ContentType,
    string? Authorization);
