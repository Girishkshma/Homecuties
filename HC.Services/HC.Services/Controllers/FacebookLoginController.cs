using HC.Business;
using Microsoft.AspNetCore.Mvc;

namespace HC.Services.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FacebookLoginController : ControllerBase
{
    private readonly IFacebookLoginService _facebookLoginService;

    public FacebookLoginController(IFacebookLoginService facebookLoginService)
    {
        _facebookLoginService = facebookLoginService;
    }

    [HttpPost("ValidateToken")]
    public async Task<ActionResult> ValidateToken([FromBody] FacebookTokenRequest request)
    {
        var result = await _facebookLoginService.ValidateTokenAsync(request.ID, request.AccessToken);
        return Ok(result);
    }
}

public class FacebookTokenRequest
{
    public string ID { get; set; } = "";
    public string AccessToken { get; set; } = "";
}
