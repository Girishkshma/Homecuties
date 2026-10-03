using System.Text.Json;

namespace HC.Tests;

/// <summary>
/// Razorpay payloads built from JSON text for the rules that read one - the charges a capture reports and the
/// answers a refund is given.
///
/// Parsed and cloned, exactly as the services hand them on: the entity outlives the document it was read from,
/// which is the one thing a caller of these readers has to get right.
/// </summary>
internal static class TestPayloads
{
    /// <summary>The payload's root element, detached from its document.</summary>
    public static JsonElement Json(string payload)
    {
        using var document = JsonDocument.Parse(payload);

        return document.RootElement.Clone();
    }
}
