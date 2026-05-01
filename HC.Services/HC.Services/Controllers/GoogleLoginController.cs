using HC.Business;
using Microsoft.AspNetCore.Mvc;

namespace HC.Services.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GoogleLoginController : ControllerBase
{
    private readonly IGoogleLoginService _googleLoginService;

    public GoogleLoginController(IGoogleLoginService googleLoginService)
    {
        _googleLoginService = googleLoginService;
    }

    [HttpPost("ValidateToken")]
    public async Task<ActionResult> ValidateToken([FromBody] GoogleTokenRequest request)
    {
        var result = await _googleLoginService.ValidateTokenAsync(request.IDToken);
        return Ok(result);
    }
}

public class GoogleTokenRequest
{
    public string IDToken { get; set; } = "";
}
