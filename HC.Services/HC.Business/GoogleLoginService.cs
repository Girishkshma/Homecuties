using HC.Business.Dtos;
using HC.Business.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HC.Business;

/// <summary>
/// Signs customers in with Google (the id_token issued by Google Identity Services on the
/// storefront login page). The token signature, issuer, audience and expiry are verified before
/// the e-mail address is trusted - see <see cref="GoogleIdTokenValidator"/>.
/// </summary>
public class GoogleLoginService : IGoogleLoginService
{
    private readonly ICustomerService _customerService;
    private readonly ILogger<GoogleLoginService> _logger;
    private readonly string _clientId;

    public GoogleLoginService(
        ICustomerService customerService,
        IConfiguration configuration,
        ILogger<GoogleLoginService> logger)
    {
        _customerService = customerService;
        _logger = logger;
        _clientId = configuration["Google:ClientId"] ?? string.Empty;
    }

    public async Task<LoginCustomerResponseDto> ValidateTokenAsync(string idToken)
    {
        if (string.IsNullOrWhiteSpace(_clientId))
        {
            _logger.LogError("Google sign-in is not configured: set 'Google:ClientId' in appsettings.json.");
            return Error("Google sign-in is not configured.");
        }

        var (googleUser, failureReason) = await GoogleIdTokenValidator.ValidateAsync(idToken, _clientId);

        if (googleUser == null)
        {
            // The reason is safe to surface (it may name the public client id, which is already in
            // the browser bundle) and turns a confusing "invalid token" into an obvious fix.
            // Trim it from the message if you would rather not disclose the category.
            _logger.LogWarning("Google sign-in rejected: {Reason}", failureReason);
            return Error($"Invalid Google token. ({failureReason})");
        }

        // Reuse/create the customer and issue the same signed token as a password login.
        return await _customerService.SignInExternalAsync(
            googleUser.Email,
            googleUser.FirstName,
            googleUser.LastName,
            googleUser.Subject);
    }

    private static LoginCustomerResponseDto Error(string message)
    {
        return new LoginCustomerResponseDto
        {
            Result = 0,
            Messages = new[] { message },
            Customer = null
        };
    }
}

