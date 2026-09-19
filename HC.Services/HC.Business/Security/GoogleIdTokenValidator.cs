using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HC.Business.Security;

/// <summary>
/// Verifies a Google Identity Services ID token (the "credential" handed back by the Google
/// Sign-In button) without pulling in an external JWT library - same spirit as
/// <see cref="CustomerTokenService"/>.
///
/// What is checked:
/// <list type="bullet">
///   <item>the token is a well formed, three part JWT with an <c>RS256</c> header and a known <c>kid</c>;</item>
///   <item>the RSA signature is valid against Google's published JWKS (re-fetched when Google rotates keys);</item>
///   <item><c>iss</c> is a Google issuer, <c>aud</c> is the configured client id and the token is not expired / not from the future;</item>
///   <item>the e-mail address is present and verified by Google.</item>
/// </list>
/// </summary>
public static class GoogleIdTokenValidator
{
    private const string JwksUrl = "https://www.googleapis.com/oauth2/v3/certs";

    private static readonly string[] ValidIssuers =
    {
        "accounts.google.com",
        "https://accounts.google.com"
    };

    /// <summary>Tolerance for small clock differences between this server and Google.</summary>
    private static readonly TimeSpan ClockSkew = TimeSpan.FromMinutes(2);

    /// <summary>How long Google's signing keys are cached before being fetched again.</summary>
    private static readonly TimeSpan KeyCacheLifetime = TimeSpan.FromHours(12);

    private static readonly HttpClient Http = new();
    private static readonly SemaphoreSlim KeyLock = new(1, 1);
    private static IReadOnlyDictionary<string, RSAParameters>? _cachedKeys;
    private static DateTime _keysCachedOn = DateTime.MinValue;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Validates <paramref name="idToken"/> for <paramref name="clientId"/>.
    /// Returns the Google account details when the token is genuine; otherwise the user is
    /// <c>null</c> and <c>FailureReason</c> explains why (short and loggable - never the token).
    /// </summary>
    public static async Task<(GoogleUserInfo? User, string? FailureReason)> ValidateAsync(
        string? idToken,
        string? clientId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(idToken) || string.IsNullOrWhiteSpace(clientId))
            return (null, "the token is empty or Google sign-in is not configured");

        var parts = idToken.Split('.');
        if (parts.Length != 3)
            return (null, "the token is not a three-part JWT");

        var header = ReadJson<GoogleJwtHeader>(parts[0]);
        var payload = ReadJson<GoogleJwtPayload>(parts[1]);

        if (header == null || payload == null)
            return (null, "the token header/payload could not be decoded");

        if (!string.Equals(header.Algorithm, "RS256", StringComparison.Ordinal) ||
            string.IsNullOrEmpty(header.KeyId))
            return (null, $"unsupported token header (alg='{header.Algorithm}', kid='{header.KeyId}')");

        var (verified, signatureReason) = await VerifySignatureAsync(parts, header.KeyId, cancellationToken);
        if (!verified)
            return (null, signatureReason);

        // Expiry / issue time (with a little clock skew tolerance).
        if (payload.ExpiresAt == null ||
            DateTimeOffset.FromUnixTimeSeconds(payload.ExpiresAt.Value) + ClockSkew <= DateTimeOffset.UtcNow)
            return (null, $"the token is expired (exp={payload.ExpiresAt})");

        if (payload.IssuedAt != null &&
            DateTimeOffset.FromUnixTimeSeconds(payload.IssuedAt.Value) - ClockSkew > DateTimeOffset.UtcNow)
            return (null, $"the token was issued in the future (iat={payload.IssuedAt})");

        if (string.IsNullOrEmpty(payload.Issuer) || !ValidIssuers.Contains(payload.Issuer))
            return (null, $"issuer mismatch (iss='{payload.Issuer}')");

        if (!AudienceMatches(payload.Audience, clientId))
            return (null,
                $"audience mismatch (token aud='{DescribeAudience(payload.Audience)}', " +
                $"configured Google:ClientId='{clientId}')");

        // The whole point of the flow: Google vouches for this mailbox.
        if (string.IsNullOrWhiteSpace(payload.Email) || payload.EmailVerified != true)
            return (null, $"email missing or not verified (email_verified={payload.EmailVerified?.ToString() ?? "absent"})");

        return (new GoogleUserInfo
        {
            Email = payload.Email.Trim(),
            FirstName = (payload.GivenName ?? string.Empty).Trim(),
            LastName = (payload.FamilyName ?? string.Empty).Trim(),
            Subject = payload.Subject ?? string.Empty
        }, null);
    }

    private static async Task<(bool Verified, string? FailureReason)> VerifySignatureAsync(
        string[] parts,
        string keyId,
        CancellationToken cancellationToken)
    {
        var keys = await GetKeysAsync(cancellationToken);

        if (keys == null)
            return (false,
                "Google's signing keys could not be loaded - check that the server can reach " +
                "https://www.googleapis.com/oauth2/v3/certs");

        if (!keys.TryGetValue(keyId, out var parameters))
        {
            // Unknown key id: Google probably rotated its keys - refresh once and retry.
            keys = await GetKeysAsync(cancellationToken, forceRefresh: true);
            if (keys == null || !keys.TryGetValue(keyId, out parameters))
                return (false, $"no Google signing key matches the token's kid='{keyId}'");
        }

        try
        {
            var signedData = Encoding.ASCII.GetBytes(parts[0] + "." + parts[1]);
            var signature = Base64UrlDecode(parts[2]);

            using var rsa = RSA.Create();
            rsa.ImportParameters(parameters);

            if (!rsa.VerifyData(signedData, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
                return (false, "the RS256 signature is invalid");
        }
        catch (Exception ex)
        {
            // Malformed base64url signature or unusable key material.
            return (false, $"the signature could not be verified ({ex.GetType().Name})");
        }

        return (true, null);
    }

    private static string DescribeAudience(JsonElement? audience)
    {
        if (audience == null)
            return "(absent)";

        return audience.Value.ValueKind switch
        {
            JsonValueKind.String => audience.Value.GetString() ?? "(empty)",
            JsonValueKind.Array => string.Join(", ", audience.Value.EnumerateArray().Select(static item => item.ToString())),
            _ => audience.Value.ToString()
        };
    }

    private static async Task<IReadOnlyDictionary<string, RSAParameters>?> GetKeysAsync(
        CancellationToken cancellationToken,
        bool forceRefresh = false)
    {
        if (!forceRefresh && _cachedKeys != null && DateTime.UtcNow - _keysCachedOn < KeyCacheLifetime)
            return _cachedKeys;

        await KeyLock.WaitAsync(cancellationToken);
        try
        {
            if (!forceRefresh && _cachedKeys != null && DateTime.UtcNow - _keysCachedOn < KeyCacheLifetime)
                return _cachedKeys;

            var json = await Http.GetStringAsync(JwksUrl, cancellationToken);
            var keySet = JsonSerializer.Deserialize<JsonWebKeySet>(json, JsonOptions);

            if (keySet?.Keys == null || keySet.Keys.Count == 0)
                return _cachedKeys;

            var keys = new Dictionary<string, RSAParameters>();
            foreach (var key in keySet.Keys)
            {
                if (!string.Equals(key.KeyType, "RSA", StringComparison.Ordinal) ||
                    string.IsNullOrEmpty(key.KeyId) ||
                    string.IsNullOrEmpty(key.Modulus) ||
                    string.IsNullOrEmpty(key.Exponent))
                    continue;

                keys[key.KeyId] = new RSAParameters
                {
                    Modulus = Base64UrlDecode(key.Modulus),
                    Exponent = Base64UrlDecode(key.Exponent)
                };
            }

            if (keys.Count > 0)
            {
                _cachedKeys = keys;
                _keysCachedOn = DateTime.UtcNow;
            }

            return _cachedKeys;
        }
        catch
        {
            // Network / parse failure: fall back to whatever was already cached (if anything).
            return _cachedKeys;
        }
        finally
        {
            KeyLock.Release();
        }
    }

    private static T? ReadJson<T>(string base64UrlPart) where T : class
    {
        try
        {
            var json = Encoding.UTF8.GetString(Base64UrlDecode(base64UrlPart));
            return JsonSerializer.Deserialize<T>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private static bool AudienceMatches(JsonElement? audience, string clientId)
    {
        if (audience == null)
            return false;

        switch (audience.Value.ValueKind)
        {
            case JsonValueKind.String:
                return string.Equals(audience.Value.GetString(), clientId, StringComparison.Ordinal);

            case JsonValueKind.Array:
                return audience.Value.EnumerateArray().Any(item =>
                    item.ValueKind == JsonValueKind.String &&
                    string.Equals(item.GetString(), clientId, StringComparison.Ordinal));

            default:
                return false;
        }
    }

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

    private sealed class JsonWebKeySet
    {
        [JsonPropertyName("keys")]
        public List<JsonWebKey>? Keys { get; set; }
    }

    private sealed class JsonWebKey
    {
        [JsonPropertyName("kty")]
        public string? KeyType { get; set; }

        [JsonPropertyName("kid")]
        public string? KeyId { get; set; }

        [JsonPropertyName("n")]
        public string? Modulus { get; set; }

        [JsonPropertyName("e")]
        public string? Exponent { get; set; }
    }

    private sealed class GoogleJwtHeader
    {
        [JsonPropertyName("alg")]
        public string? Algorithm { get; set; }

        [JsonPropertyName("kid")]
        public string? KeyId { get; set; }
    }

    private sealed class GoogleJwtPayload
    {
        [JsonPropertyName("iss")]
        public string? Issuer { get; set; }

        /// <summary>Usually the client id as a string, kept as a JSON element to also accept an array.</summary>
        [JsonPropertyName("aud")]
        public JsonElement? Audience { get; set; }

        [JsonPropertyName("sub")]
        public string? Subject { get; set; }

        [JsonPropertyName("email")]
        public string? Email { get; set; }

        [JsonPropertyName("email_verified")]
        public bool? EmailVerified { get; set; }

        [JsonPropertyName("given_name")]
        public string? GivenName { get; set; }

        [JsonPropertyName("family_name")]
        public string? FamilyName { get; set; }

        [JsonPropertyName("iat")]
        public long? IssuedAt { get; set; }

        [JsonPropertyName("exp")]
        public long? ExpiresAt { get; set; }
    }
}

/// <summary>The Google account details taken from a verified ID token.</summary>
public sealed class GoogleUserInfo
{
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;

    /// <summary>Google's stable account id (<c>sub</c> claim) - stored against the customer.</summary>
    public string Subject { get; set; } = string.Empty;
}


