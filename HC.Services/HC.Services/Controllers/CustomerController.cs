using HC.Business;
using HC.Business.Dtos;
using HC.Business.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace HC.Services.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CustomerController : ControllerBase
{
    private const string DefaultJwtSecret = "123456789abcdefgh";

    private readonly ICustomerService _customerService;
    private readonly IPincodeService _pincodeService;
    private readonly string _jwtSecret;

    public CustomerController(ICustomerService customerService, IPincodeService pincodeService, IConfiguration configuration)
    {
        _customerService = customerService;
        _pincodeService = pincodeService;
        _jwtSecret = configuration["JWTSecret"] ?? DefaultJwtSecret;
    }

    [HttpPost("GetCustomer")]
    public async Task<ActionResult> GetCustomer([FromBody] GetCustomerRequest request)
    {
        var customer = await _customerService.GetCustomerAsync(request.CustomerID);

        if (customer == null)
            return NotFound(new { Result = 0, Messages = new[] { "Customer not found" } });

        return Ok(customer);
    }

    [HttpPost("GetCustomerJWT")]
    public ActionResult GetCustomerJWT([FromBody] JwtRequest request)
    {
        var result = _customerService.GetCustomerJwt(request.Customer.CustomerID, request.Customer.EmailId, request.IPAddress);
        return Ok(result);
    }

    [HttpPost("ValidateCustomerJWT")]
    public ActionResult ValidateCustomerJWT([FromBody] ValidateJwtRequest request)
    {
        var result = _customerService.ValidateCustomerJwt(request.JWT);
        return Ok(result);
    }

    [HttpPost("CreatetGuestCustomer")]
    public async Task<ActionResult> CreateGuestCustomer()
    {
        var result = await _customerService.CreateGuestCustomerAsync();
        return Ok(result);
    }

    [HttpPost("CreateCustomer")]
    public async Task<ActionResult> CreateCustomer([FromBody] CreateCustomerRequest request)
    {
        var result = await _customerService.CreateCustomerAsync(
            request.FirstName, request.LastName, request.Email, request.Password);
        return Ok(result);
    }

    [HttpPost("Login")]
    public async Task<ActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await _customerService.LoginAsync(request.Email, request.Password);
        return Ok(result);
    }

    [HttpPost("ForgotPassword")]
    public async Task<ActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        var result = await _customerService.ForgotPasswordAsync(request.Email);
        return Ok(result);
    }

    [HttpPost("ResetPassword")]
    public async Task<ActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        var result = await _customerService.ResetPasswordAsync(request.Token, request.NewPassword);
        return Ok(result);
    }

    /// <summary>
    /// Sets the internal password of the signed-in customer - used right after a first Google
    /// sign-in (which creates the account without a password) and for later password changes.
    /// </summary>
    [HttpPost("SetPassword")]
    public async Task<ActionResult> SetPassword([FromBody] SetPasswordRequest request)
    {
        // The customer is identified by the signed-in token, never by the request body.
        var customerId = GetTokenCustomerId();
        if (customerId == null)
            return Unauthorized(new { Result = 0, Messages = new[] { "Please sign in again." } });

        var result = await _customerService.SetPasswordAsync(customerId.Value, request.CurrentPassword, request.NewPassword);
        return Ok(result);
    }

    /// <summary>
    /// The signed-in customer's address book. The customer is identified by the token, so nothing
    /// identifying is sent from the storefront and one customer can never read another's addresses.
    /// </summary>
    [HttpPost("GetAddresses")]
    public async Task<ActionResult> GetAddresses()
    {
        var customerId = GetTokenCustomerId();
        if (customerId == null)
            return Unauthorized(new { Result = 0, Messages = new[] { "Please sign in to see your addresses." } });

        var addresses = await _customerService.GetAddressesAsync(customerId.Value);
        return Ok(addresses);
    }

    /// <summary>
    /// Adds an address to the signed-in customer's address book, or updates one that is already in it
    /// (AddressId &gt; 0). The saved address is then offered in checkout as the shipping or the billing
    /// address.
    /// </summary>
    [HttpPost("SaveAddress")]
    public async Task<ActionResult> SaveAddress([FromBody] SaveCustomerAddressRequest request)
    {
        var customerId = GetTokenCustomerId();
        if (customerId == null)
            return Unauthorized(new { Result = 0, Messages = new[] { "Please sign in to save an address." } });

        var result = await _customerService.SaveAddressAsync(customerId.Value, request);
        return Ok(result);
    }

    /// <summary>
    /// Removes an address from the signed-in customer's address book. An address an order was placed
    /// with is refused - the order would lose the address it was shipped to.
    /// </summary>
    [HttpPost("DeleteAddress")]
    public async Task<ActionResult> DeleteAddress([FromBody] DeleteCustomerAddressRequest request)
    {
        var customerId = GetTokenCustomerId();
        if (customerId == null)
            return Unauthorized(new { Result = 0, Messages = new[] { "Please sign in to remove an address." } });

        var result = await _customerService.DeleteAddressAsync(customerId.Value, request.AddressId);
        return Ok(result);
    }

    /// <summary>
    /// The city, the state and the areas a PIN code covers, so the address forms can fill those in
    /// instead of asking the customer to type them ('PIN code -> city, state and area').
    ///
    /// Deliberately open to everyone: checkout also runs for guests, and a PIN code is public
    /// information. An unknown PIN or an unreachable directory is answered with Result = 0 and a reason,
    /// never an error status - the customer types the address themselves and carries on.
    /// </summary>
    [HttpGet("GetPincode/{pincode}")]
    public async Task<ActionResult> GetPincode(string pincode, CancellationToken cancellationToken)
    {
        var result = await _pincodeService.LookupAsync(pincode, cancellationToken);
        return Ok(result);
    }

    /// <summary>Reads "Authorization: Bearer &lt;token&gt;" and returns the signed-in customer id when it is valid.</summary>
    private long? GetTokenCustomerId()
    {
        var header = Request.Headers["Authorization"].ToString();
        if (string.IsNullOrWhiteSpace(header) || !header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return null;

        return CustomerTokenService.Validate(header["Bearer ".Length..].Trim(), _jwtSecret);
    }
}

public class GetCustomerRequest
{
    public long CustomerID { get; set; }
}

public class JwtRequest
{
    public CustomerData Customer { get; set; } = null!;
    public string IPAddress { get; set; } = "";
}

public class CustomerData
{
    public long CustomerID { get; set; }
    public string EmailId { get; set; } = "";
}

public class ValidateJwtRequest
{
    public string JWT { get; set; } = "";
    public string IPAddress { get; set; } = "";
}

public class CreateCustomerRequest
{
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
}

public class LoginRequest
{
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
}

public class ForgotPasswordRequest
{
    public string Email { get; set; } = "";
}

public class ResetPasswordRequest
{
    public string Token { get; set; } = "";
    public string NewPassword { get; set; } = "";
}

public class SetPasswordRequest
{
    /// <summary>Only required when the account already has a password (i.e. when changing it).</summary>
    public string? CurrentPassword { get; set; }
    public string NewPassword { get; set; } = "";
}
