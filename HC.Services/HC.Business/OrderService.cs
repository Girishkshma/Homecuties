using System.Security.Cryptography;
using System.Text;
using HC.Business.Dtos;
using HC.Data;
using HC.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HC.Business;

public partial class OrderService : IOrderService
{
    // SKUStatuses.SKUStatusID = 1 => "Available", 4 => "Ordered". The values live in SkuAvailability so
    // that every storefront query shares one definition of "this unit can be sold" (a unit of a
    // cancelled order goes back to Available and can be sold again).
    private const short AvailableSkuStatusId = SkuAvailability.AvailableSkuStatusId;
    private const short OrderedSkuStatusId = SkuAvailability.OrderedSkuStatusId;

    private readonly HomecutiesDbContext _context;
    private readonly ILogger<OrderService> _logger;

    /// <summary>Storefront payment gateway (Razorpay) credentials - empty when not configured.</summary>
    private readonly string _razorpayKeyId;
    private readonly string _razorpayKeySecret;
    private readonly string _razorpayWebhookSecret;

    public OrderService(HomecutiesDbContext context, IConfiguration configuration, ILogger<OrderService> logger)
    {
        _context = context;
        _logger = logger;
        _razorpayKeyId = configuration["Razorpay:KeyId"] ?? string.Empty;
        _razorpayKeySecret = configuration["Razorpay:KeySecret"] ?? string.Empty;
        _razorpayWebhookSecret = configuration["Razorpay:WebhookSecret"] ?? string.Empty;
    }

    /// <summary>True when both Razorpay credentials are present (the gateway can be used).</summary>
    private bool IsRazorpayConfigured =>
        !string.IsNullOrWhiteSpace(_razorpayKeyId) && !string.IsNullOrWhiteSpace(_razorpayKeySecret);

}
