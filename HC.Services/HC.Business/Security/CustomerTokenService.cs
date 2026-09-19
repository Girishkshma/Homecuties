using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace HC.Business.Security;

/// <summary>
/// Small HMAC-SHA256 signed token for storefront customers - no external JWT dependency needed.
/// Format: <c>base64url(payloadJson).base64url(HMACSHA256(payloadJson, secret))</c>.
/// Payload: <c>{ "Cid": long, "Email": string, "Exp": unixSeconds }</c>.
/// </summary>
public static class CustomerTokenService
{
    public const int DefaultLifetimeDays = 7;

    public static (string Token, DateTime ExpiresOn) Create(
        long customerId,
        string email,
        string secret,
        int lifetimeDays = DefaultLifetimeDays)
    {
        var expiresOn = DateTime.UtcNow.AddDays(lifetimeDays);

        var payload = JsonSerializer.Serialize(new TokenPayload
        {
            Cid = customerId,
            Email = email ?? "",
            Exp = new DateTimeOffset(expiresOn).ToUnixTimeSeconds()
        });

        var payloadPart = Base64UrlEncode(Encoding.UTF8.GetBytes(payload));
        var signaturePart = Sign(payloadPart, secret);

        return ($"{payloadPart}.{signaturePart}", expiresOn);
    }

    /// <summary>Returns the customer id when the token is well formed, correctly signed and not expired; otherwise null.</summary>
    public static long? Validate(string? token, string secret)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        var parts = token.Split('.');
        if (parts.Length != 2)
            return null;

        if (!FixedTimeEquals(parts[1], Sign(parts[0], secret)))
            return null;

        try
        {
            var json = Encoding.UTF8.GetString(Base64UrlDecode(parts[0]));
            var payload = JsonSerializer.Deserialize<TokenPayload>(json);

            if (payload == null || payload.Cid <= 0)
                return null;

            if (DateTimeOffset.FromUnixTimeSeconds(payload.Exp) < DateTimeOffset.UtcNow)
                return null;

            return payload.Cid;
        }
        catch
        {
            return null;
        }
    }

    private static string Sign(string payloadPart, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return Base64UrlEncode(hmac.ComputeHash(Encoding.UTF8.GetBytes(payloadPart)));
    }

    private static bool FixedTimeEquals(string? a, string? b)
    {
        var bytesA = Encoding.UTF8.GetBytes(a ?? "");
        var bytesB = Encoding.UTF8.GetBytes(b ?? "");
        return bytesA.Length == bytesB.Length && CryptographicOperations.FixedTimeEquals(bytesA, bytesB);
    }

    private static string Base64UrlEncode(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string value)
    {
        var normalized = value.Replace('-', '+').Replace('_', '/');
        normalized = (normalized.Length % 4) switch
        {
            2 => normalized + "==",
            3 => normalized + "=",
            _ => normalized
        };
        return Convert.FromBase64String(normalized);
    }

    private sealed class TokenPayload
    {
        public long Cid { get; set; }
        public string Email { get; set; } = "";
        public long Exp { get; set; }
    }
}
