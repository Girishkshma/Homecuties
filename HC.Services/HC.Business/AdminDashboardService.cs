using System.Security.Cryptography;
using System.Text;
using HC.Business.Dtos;
using HC.Business.Shipping;
using HC.Data;
using HC.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace HC.Business;

public partial class AdminDashboardService : IAdminDashboardService
{
    private readonly HomecutiesDbContext _context;
    private readonly string _pwdSecret;
    private readonly string _razorpayKeyId;
    private readonly string _razorpayKeySecret;

    /// <summary>
    /// The parcel side of an order (see HC.Business.Shipping): read for the order screen's Shipment card
    /// and written when the shop team records a parcel or asks the courier about one. Going through the
    /// tracking service is what keeps this class free of any one provider's vocabulary, and what makes a
    /// courier that cannot be reached a sentence on the card instead of an error on the page.
    /// </summary>
    private readonly IShipmentTrackingService _shipmentTracking;

    public AdminDashboardService(
        HomecutiesDbContext context,
        IConfiguration configuration,
        IShipmentTrackingService shipmentTracking)
    {
        _context = context;
        _shipmentTracking = shipmentTracking;
        _pwdSecret = configuration["PWDSecret"] ?? "abcd1234!@#$";

        // Cancelling an order gives the money back through the same Razorpay credentials the checkout
        // uses (Razorpay:KeyId / Razorpay:KeySecret in appsettings).
        _razorpayKeyId = configuration["Razorpay:KeyId"] ?? string.Empty;
        _razorpayKeySecret = configuration["Razorpay:KeySecret"] ?? string.Empty;
    }

}
