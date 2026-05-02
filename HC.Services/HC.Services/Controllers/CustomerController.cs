using HC.Business;
using Microsoft.AspNetCore.Mvc;

namespace HC.Services.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CustomerController : ControllerBase
{
    private readonly ICustomerService _customerService;

    public CustomerController(ICustomerService customerService)
    {
        _customerService = customerService;
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
