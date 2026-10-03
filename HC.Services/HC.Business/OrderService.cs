using System.Security.Cryptography;
using System.Text;
using HC.Business.Dtos;
using HC.Business.Shipping;
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

    /// <summary>
    /// The parcel side of an order (see HC.Business.Shipping): recorded once the shop has booked it with
    /// a courier, read here for 'My Orders' and refreshed by the throttled pull the page asks for. Going
    /// through the tracking service is what keeps this class free of any one provider's vocabulary.
    /// </summary>
    private readonly IShipmentTrackingService _shipmentTracking;

    /// <summary>Storefront payment gateway (Razorpay) credentials - empty when not configured.</summary>
    private readonly string _razorpayKeyId;
    private readonly string _razorpayKeySecret;
    private readonly string _razorpayWebhookSecret;

    /// <summary>
    /// How long a customer has to ask for a return (<c>Returns:WindowDays</c>), counted from the day the
    /// parcel reached them - see <see cref="OrderReturnFlow"/>. It is the customer-facing window only: the
    /// admin area never checks it, because the shop team may raise or accept a return whenever it is right
    /// to.
    /// </summary>
    private readonly int _returnWindowDays;

    public OrderService(
        HomecutiesDbContext context,
        IConfiguration configuration,
        ILogger<OrderService> logger,
        IShipmentTrackingService shipmentTracking)
    {
        _context = context;
        _logger = logger;
        _shipmentTracking = shipmentTracking;
        _razorpayKeyId = configuration["Razorpay:KeyId"] ?? string.Empty;
        _razorpayKeySecret = configuration["Razorpay:KeySecret"] ?? string.Empty;
        _razorpayWebhookSecret = configuration["Razorpay:WebhookSecret"] ?? string.Empty;

        // A missing or unreadable setting falls back to the documented default rather than to no window at
        // all: a return is a promise to the customer, and it must not disappear because a key is misspelt.
        _returnWindowDays = int.TryParse(configuration["Returns:WindowDays"], out var windowDays) && windowDays >= 0
            ? windowDays
            : OrderReturnFlow.DefaultWindowDays;
    }

    /// <summary>True when both Razorpay credentials are present (the gateway can be used).</summary>
    private bool IsRazorpayConfigured =>
        !string.IsNullOrWhiteSpace(_razorpayKeyId) && !string.IsNullOrWhiteSpace(_razorpayKeySecret);

}
