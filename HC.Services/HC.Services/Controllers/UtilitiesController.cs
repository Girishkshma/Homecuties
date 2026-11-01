using HC.Business;
using HC.Business.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace HC.Services.Controllers;

/// <summary>
/// The calls a shop front makes that are not about a product, an order or a customer - today, one: a page
/// was opened.
///
/// 'AddAccessTracking' is what the storefront calls on every page load (see HC.Web/src/app/app.component.ts),
/// and what it records is where the reader's address is and when - never who. Three things about it are
/// deliberate:
///
///   - The place comes off the connection, not out of the body: the first hop of 'X-Forwarded-For' when the
///     API sits behind a proxy or a load balancer, and the connection's own remote address otherwise. Any
///     place an older storefront still sends in the body is ignored, so a browser cannot file itself in a
///     country it is not in.
///   - The one thing a browser may add is a point the visitor agreed to share ('latitude', 'longitude',
///     'accuracyMeters' - see HC.Web/src/app/geo-consent). It is optional, it is checked and rounded before it
///     is stored (HC.Business.AccessTrackingRules.ResolveSharedPoint), and it is written beside the place the
///     address was looked up to rather than in place of it: a coordinate can add to the picture, never
///     redirect it. A visitor who declines, whose browser cannot tell, or whose device takes too long is
///     recorded exactly as every visitor was before the shop asked.
///   - It always answers 200 with a result and never an error: tracking is not worth a page, so a hit that
///     could not be recorded is said in the body and the shop carries on loading.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class UtilitiesController : ControllerBase
{
    private readonly IAccessTrackingService _accessTrackingService;

    public UtilitiesController(IAccessTrackingService accessTrackingService)
    {
        _accessTrackingService = accessTrackingService;
    }

    [HttpPost("AddAccessTracking")]
    public async Task<ActionResult> AddAccessTracking([FromBody] AccessTrackingRequestDto? request)
    {
        // The first page of a session carries no 'Referer', and an older storefront posts no body at all: the
        // page that was opened is taken from the body where it is there, and the header is the fallback it
        // has always been.
        request ??= new AccessTrackingRequestDto();
        request.PageUrl ??= Request.Headers["Referer"].FirstOrDefault();

        var result = await _accessTrackingService.TrackAsync(request, GetClientIp(), HttpContext.RequestAborted);
        return Ok(result);
    }

    /// <summary>
    /// The caller's address: the leftmost entry of 'X-Forwarded-For' (the browser's own address - the entries
    /// after it are the proxies it passed through), and the connection's remote address when no proxy filled
    /// the header in.
    /// </summary>
    private string GetClientIp()
    {
        if (Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor))
        {
            var forwarded = forwardedFor.FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(forwarded))
                return forwarded.Split(',').First().Trim();
        }

        return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "";
    }
}

