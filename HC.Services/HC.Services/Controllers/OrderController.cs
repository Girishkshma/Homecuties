using HC.Business;
using HC.Business.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace HC.Services.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrderController : ControllerBase
{
    private readonly IOrderService _orderService;

    public OrderController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    [HttpPost("CreateOrder")]
    public async Task<ActionResult> CreateOrder([FromBody] CreateOrderRequest request)
    {
        var result = await _orderService.CreateOrderAsync(request);
        return Ok(result);
    }

    [HttpPost("VerifyPayment")]
    public async Task<ActionResult> VerifyPayment([FromBody] VerifyPaymentRequest request)
    {
        var result = await _orderService.VerifyPaymentAsync(request);
        return Ok(result);
    }

    [HttpPost("GetOrders")]
    public async Task<ActionResult> GetOrders([FromBody] CartRequest request)
    {
        var result = await _orderService.GetOrdersAsync(request.CustomerID, request.IsGuest);
        return Ok(result);
    }
}
