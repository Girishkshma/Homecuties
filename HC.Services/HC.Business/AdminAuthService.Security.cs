// ============================================================
// AdminAuthService.Security.cs
// Partial class: AdminAuthService - Security operations
// ============================================================

using System.Security.Cryptography;
using System.Text;

namespace HC.Business;

public partial class AdminAuthService : IAdminAuthService
{
    /// <summary>
    /// Creates an HMACSHA256 hash of the input string using the given secret key.
    /// Matches the old HC.Common.Crypto.CreateHMACSHA256Token implementation.
    /// Still used for the stored password hashes and for the password reset tokens; the admin
    /// session itself now uses a standard, signed JWT - see <c>AdminJwtTokenService</c>.
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

}
