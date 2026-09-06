// ============================================================
// AdminAuthService.Security.cs
// Partial class: AdminAuthService - Security operations
// ============================================================

using HC.Business.Dtos;
using HC.Data;
using HC.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace HC.Business;

public partial class AdminAuthService : IAdminAuthService
{
    /// <summary>
    /// Creates an HMACSHA256 hash of the input string using the given secret key.
    /// Matches the old HC.Common.Crypto.CreateHMACSHA256Token implementation.
    /// </summary>
    private static string CreateHMACSHA256Token(string message, string secret)
    {
        secret = secret ?? "";
        var encoding = new ASCIIEncoding();
        byte[] keyByte = encoding.GetBytes(secret);
        byte[] messageBytes = encoding.GetBytes(message);
        using (var hmacsha256 = new HMACSHA256(keyByte))
        {
            byte[] hashmessage = hmacsha256.ComputeHash(messageBytes);
            return Convert.ToBase64String(hashmessage);
        }
    }

    /// <summary>
    /// Encrypts a password using HMACSHA256 with the PWDSecret.
    /// Matches the old app's password encryption: CreateHMACSHA256Token(password, PWDSecret)
    /// </summary>
    private string EncryptPassword(string password)
    {
        return CreateHMACSHA256Token(password, _pwdSecret);
    }

    /// <summary>
    /// Generates a JWT token matching the old app's format:
    /// base64(header).base64(body).HMACSHA256(base64(header)+"."+base64(body), JWTSecret)
    /// </summary>
    private string GenerateJwtToken(long userId, string loginId, string ipAddress)
    {
        var header = new { Type = "JWT", Crypto = "HMACSHA256" };
        var now = DateTime.UtcNow; // All token timestamps are UTC
        var body = new
        {
            UserId = userId,
            LoginId = loginId,
            IPAddress = ipAddress,
            Created = now.ToString("yyyyMMddHHmmss"),
            Expires = now.AddHours(1).ToString("yyyyMMddHHmmss")
        };

        var headerSer = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(header)));
        var bodySer = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(body)));

        var token = CreateHMACSHA256Token(headerSer + "." + bodySer, _jwtSecret);

        return headerSer + "." + bodySer + "." + token;
    }

    /// <summary>
    /// Validates a JWT token matching the old app's format.
    /// Returns the deserialized payload if valid, null otherwise.
    /// </summary>
    private JsonElement? ValidateJwtToken(string jwt, string ipAddress)
    {
        try
        {
            var ar = jwt.Split('.');
            if (ar.Length != 3)
                return null;

            // Verify signature
            var expectedSig = CreateHMACSHA256Token(ar[0] + "." + ar[1], _jwtSecret);
            if (expectedSig != ar[2])
                return null;

            // Decode payload
            var payloadJson = Encoding.UTF8.GetString(Convert.FromBase64String(ar[1]));
            var payload = JsonSerializer.Deserialize<JsonElement>(payloadJson);

            // Verify IP address
            if (payload.TryGetProperty("IPAddress", out var ipProp))
            {
                if (ipProp.GetString() != ipAddress)
                    return null;
            }

            // Verify expiration
            if (payload.TryGetProperty("Expires", out var expProp))
            {
                var expStr = expProp.GetString();
                if (!string.IsNullOrEmpty(expStr))
                {
                    if (DateTime.ParseExact(expStr, "yyyyMMddHHmmss", null) < DateTime.UtcNow)
                        return null;
                }
            }

            return payload;
        }
        catch
        {
            return null;
        }
    }

}
