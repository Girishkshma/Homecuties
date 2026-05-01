using HC.Business;
using Microsoft.AspNetCore.Mvc;

namespace HC.Services.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UtilitiesController : ControllerBase
{
    private readonly IUtilitiesService _utilitiesService;

    public UtilitiesController(IUtilitiesService utilitiesService)
    {
        _utilitiesService = utilitiesService;
    }

    [HttpPost("AddAccessTracking")]
    public async Task<ActionResult> AddAccessTracking([FromBody] object data)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var url = Request.Headers["Referer"].FirstOrDefault() ?? "unknown";
        var result = await _utilitiesService.AddAccessTrackingAsync(ip, url);
        return Ok(result);
    }
}
