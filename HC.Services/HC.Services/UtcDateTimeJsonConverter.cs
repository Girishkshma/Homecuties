using System.Text.Json;
using System.Text.Json.Serialization;

namespace HC.Services;

/// <summary>
/// Serializes all DateTimes as UTC (with the 'Z' suffix) regardless of the value's Kind,
/// and treats incoming date strings as UTC. Stored SQL datetime values (Kind=Unspecified)
/// are interpreted as UTC so clients receive a true UTC instant.
/// </summary>
public sealed class UtcDateTimeJsonConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.GetDateTime().ToUniversalTime();
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        var utc = value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
        writer.WriteStringValue(utc);
    }
}
