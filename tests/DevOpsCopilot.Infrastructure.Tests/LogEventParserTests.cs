using System.Text;
using System.Text.Json;
using DevOpsCopilot.Infrastructure;

namespace DevOpsCopilot.Infrastructure.Tests;

public class LogEventParserTests
{
    [Fact]
    public void Parse_reads_the_log_fields_and_w3c_trace_context()
    {
        // Arrange
        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        {
            Timestamp = DateTimeOffset.Parse("2026-10-20T10:00:00Z"),
            Level = "Error",
            Category = "Checkout.Api",
            Message = "checkout failed",
            Exception = "TimeoutException: timed out",
            TraceId = "4bf92f3577b34da6a3ce929d0e0e4736",
            SpanId = "00f067aa0ba902b7",
            Attribute = new Dictionary<string, string> { ["operation"] = "checkout" },
        }));

        // Act
        var logEvent = LogEventParser.Parse(body);

        // Assert
        Assert.Equal("Error", logEvent.Level);
        Assert.Equal("Checkout.Api", logEvent.Category);
        Assert.Equal("checkout failed", logEvent.Message);
        Assert.Equal("TimeoutException: timed out", logEvent.Exception);
        Assert.Equal("4bf92f3577b34da6a3ce929d0e0e4736", logEvent.TraceId);
        Assert.Equal("00f067aa0ba902b7", logEvent.SpanId);
        Assert.Equal("checkout", logEvent.Attribute!["operation"]);
    }
}
