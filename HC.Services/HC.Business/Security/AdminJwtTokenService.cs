using HC.Business.Dtos;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace HC.Business.Security;

/// <summary>
/// Issues and validates the JSON Web Token (JWT) used by the admin area.
///
/// The token is created when an admin logs in and must be sent back on every single admin API call
/// in the 'Authorization: Bearer &lt;token&gt;' header. The API validates the signature, the issuer, the
/// audience and the expiry before a controller action is allowed to run, and reads the roles the
/// admin was granted from the token to authorize the individual sections of the admin area.
///
/// Claims
///   userId, loginId, name, email - who signed in
///   role, roleId                 - one claim per active role (used for section authorization)
///   iat, nbf                     - when the admin logged in (UTC)
///   exp                          - when the session ends (UTC)
///   iss, aud, jti                - issuer, audience and a unique token id
///
/// Settings (appsettings.json, appsettings.Production.json, appsettings.Local.json, environment
/// variables such as 'Jwt__Key' or command line arguments):
///   Jwt:Key            signing key/secret - falls back to the legacy 'JWTSecret' entry
///   Jwt:Issuer         default 'homecuties-admin'
///   Jwt:Audience       default 'homecuties-admin-api'
///   Jwt:ExpiryMinutes  default 60 (one hour)
/// </summary>
public static class AdminJwtTokenService
{
    /// <summary>Id of the signed-in admin (the 'Users.UserID' value).</summary>
    public const string UserIdClaim = "userId";
    /// <summary>Login id the admin signed in with.</summary>
    public const string LoginIdClaim = "loginId";
    /// <summary>Display name of the admin.</summary>
    public const string NameClaim = "name";
    /// <summary>E-mail id of the admin (may be empty).</summary>
    public const string EmailClaim = "email";
    /// <summary>Role name - repeated once per active role, this is what section authorization checks.</summary>
    public const string RoleClaim = "role";
    /// <summary>Role id ('Roles.RoleID') - repeated once per active role.</summary>
    public const string RoleIdClaim = "roleId";

    public const int DefaultExpiryMinutes = 60;
    public const string DefaultIssuer = "homecuties-admin";
    public const string DefaultAudience = "homecuties-admin-api";

    /// <summary>HMAC-SHA256 keys shorter than 256 bits are technically usable but weak.</summary>
    public const int RecommendedKeyLengthInBytes = 32;

    /// <summary>Signing key. Prefers 'Jwt:Key' and falls back to the legacy 'JWTSecret' entry.</summary>
    public static string GetSigningKey(IConfiguration configuration)
    {
        var key = configuration["Jwt:Key"];
        if (string.IsNullOrWhiteSpace(key))
            key = configuration["JWTSecret"];

        if (string.IsNullOrWhiteSpace(key))
            throw new InvalidOperationException(
                "No JWT signing key is configured. Set 'Jwt:Key' (or the legacy 'JWTSecret') in " +
                "appsettings.json / appsettings.Production.json / appsettings.Local.json or in the " +
                "environment variable 'Jwt__Key'.");

        return key.Trim();
    }

    /// <summary>True when the configured key is shorter than the recommended 256 bits.</summary>
    public static bool IsWeakKey(string key) => Encoding.UTF8.GetByteCount(key) < RecommendedKeyLengthInBytes;

    public static string GetIssuer(IConfiguration configuration)
        => string.IsNullOrWhiteSpace(configuration["Jwt:Issuer"]) ? DefaultIssuer : configuration["Jwt:Issuer"]!.Trim();

    public static string GetAudience(IConfiguration configuration)
        => string.IsNullOrWhiteSpace(configuration["Jwt:Audience"]) ? DefaultAudience : configuration["Jwt:Audience"]!.Trim();

    /// <summary>Session lifetime in minutes ('Jwt:ExpiryMinutes', default 60).</summary>
    public static int GetExpiryMinutes(IConfiguration configuration)
        => int.TryParse(configuration["Jwt:ExpiryMinutes"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var minutes) && minutes > 0
            ? minutes
            : DefaultExpiryMinutes;

    /// <summary>
    /// Creates the signed JWT for a successfully authenticated admin, together with the UTC instant
    /// at which it expires.
    /// </summary>
    public static (string Token, DateTime ExpiresOn) Create(
        AdminUserDto user,
        IEnumerable<AdminRoleDto> roles,
        IConfiguration configuration)
    {
        var now = DateTime.UtcNow;
        var expiresOn = now.AddMinutes(GetExpiryMinutes(configuration));

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.UserId.ToString(CultureInfo.InvariantCulture)),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(JwtRegisteredClaimNames.Iat, new DateTimeOffset(now).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture), ClaimValueTypes.Integer64),
            new(UserIdClaim, user.UserId.ToString(CultureInfo.InvariantCulture)),
            new(LoginIdClaim, user.LoginId ?? string.Empty),
            new(NameClaim, BuildDisplayName(user)),
            new(EmailClaim, user.EmailId ?? string.Empty)
        };

        // One role claim per active role - the admin API turns these into the sections the admin may open.
        foreach (var role in roles.Where(r => r.RoleId != 0).DistinctBy(r => r.RoleId))
        {
            claims.Add(new Claim(RoleClaim, role.RoleName ?? string.Empty));
            claims.Add(new Claim(RoleIdClaim, role.RoleId.ToString(CultureInfo.InvariantCulture)));
        }

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(GetSigningKey(configuration)));
        var token = new JwtSecurityToken(
            issuer: GetIssuer(configuration),
            audience: GetAudience(configuration),
            claims: claims,
            notBefore: now,
            expires: expiresOn,
            signingCredentials: new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256));

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresOn);
    }

    /// <summary>
    /// The validation rules applied to every incoming admin token: HS256 signature, issuer,
    /// audience, expiry (with a 30 second clock skew) and the claim names used above.
    /// </summary>
    public static TokenValidationParameters CreateValidationParameters(IConfiguration configuration)
        => new()
        {
            ValidateIssuer = true,
            ValidIssuer = GetIssuer(configuration),
            ValidateAudience = true,
            ValidAudience = GetAudience(configuration),
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(GetSigningKey(configuration))),
            ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },
            RequireSignedTokens = true,
            RequireExpirationTime = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = NameClaim,
            RoleClaimType = RoleClaim
        };

    /// <summary>
    /// Validates a raw token (signature, issuer, audience, lifetime) and returns its claims
    /// principal, or null when the token is missing, malformed, forged or expired.
    /// Callers must still confirm that the account itself is active.
    /// </summary>
    public static ClaimsPrincipal? Validate(string? token, IConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        try
        {
            var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
            return handler.ValidateToken(token, CreateValidationParameters(configuration), out _);
        }
        catch
        {
            // Any validation failure (bad signature, expired, wrong issuer/audience, malformed) means 'not authenticated'.
            return null;
        }
    }

    /// <summary>Id of the signed-in admin carried by the token, or 0 when it is absent/unreadable.</summary>
    public static long GetUserId(ClaimsPrincipal? principal)
    {
        var value = principal?.FindFirst(UserIdClaim)?.Value ?? principal?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var userId) ? userId : 0;
    }

    /// <summary>Role ids carried by the token - the input of the admin area section authorization.</summary>
    public static List<short> GetRoleIds(ClaimsPrincipal? principal)
    {
        if (principal == null)
            return new List<short>();

        return principal.FindAll(RoleIdClaim)
            .Select(claim => short.TryParse(claim.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var roleId) ? roleId : (short)0)
            .Where(roleId => roleId > 0)
            .Distinct()
            .ToList();
    }

    /// <summary>Role names carried by the token.</summary>
    public static List<string> GetRoleNames(ClaimsPrincipal? principal)
        => principal == null ? new List<string>() : principal.FindAll(RoleClaim).Select(claim => claim.Value).Distinct().ToList();

    private static string BuildDisplayName(AdminUserDto user)
    {
        var name = string.Join(' ', new[] { user.FirstName, user.MiddleName, user.LastName }
            .Where(part => !string.IsNullOrWhiteSpace(part)));
        return string.IsNullOrWhiteSpace(name) ? (user.LoginId ?? string.Empty) : name;
    }
}
