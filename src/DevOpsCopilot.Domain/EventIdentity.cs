using System.Globalization;

namespace DevOpsCopilot.Domain;

/// <summary>The deterministic event id derived from an event's timestamp.</summary>
public static class EventIdentity
{
    private const string Prefix = "evt-";
    private const string Format = "yyyyMMddHHmmssfff";

    public static string EventId(DateTimeOffset timestamp)
    {
        return Prefix + timestamp.ToUniversalTime().ToString(Format, CultureInfo.InvariantCulture);
    }

    public static DateTimeOffset ParseEventId(string eventId)
    {
        var compact = eventId.StartsWith(Prefix, StringComparison.Ordinal) ? eventId[Prefix.Length..] : eventId;
        return DateTimeOffset.ParseExact(compact, Format, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
    }
}
