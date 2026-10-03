// ============================================================
// AdminDashboardService.Shipments.cs
// Partial class: AdminDashboardService - the parcel of an order, for the admin order screen
// ============================================================

using HC.Business.Dtos;
using HC.Business.Shipping;

namespace HC.Business;

public partial class AdminDashboardService : IAdminDashboardService
{
    /// <summary>
    /// The providers this shop is set up with, for the Shipment card's provider list. It comes from the
    /// registry (see HC.Business.Shipping), so it stays honest: an adapter that is registered but has no
    /// credentials is shown as not configured rather than offered as if it worked, and the one a parcel
    /// defaults to is marked.
    /// </summary>
    public Task<List<ShipmentProviderInfo>> GetShipmentProvidersAsync()
        => Task.FromResult(_shipmentTracking.GetProviders().ToList());

    /// <summary>
    /// Records (or corrects) the parcel of an order: the AWB the shop team booked in the provider's
    /// panel, the courier and tracking link when they were given them, and what the courier billed for
    /// the parcel (the shop's own figure, never shown to the customer). The AWB is written to the
    /// order's history, so the order timeline itself says who recorded the parcel and when.
    ///
    /// It never moves the order: booking a parcel is not dispatching it, and the order follows only the
    /// courier's own reports (see <see cref="TrackOrderShipmentAsync"/>). A provider that is not set up,
    /// or an order that does not exist, comes back as a sentence in <c>Messages</c> with Result 0 - the
    /// same answer shape every other admin action uses, so the card shows why rather than failing.
    /// </summary>
    public Task<OrderShipmentDto> SaveOrderShipmentAsync(long orderId, SaveOrderShipmentRequest request, long currentUserId)
        => _shipmentTracking.SaveAsync(orderId, request, currentUserId);

    /// <summary>
    /// 'Track now' on the Shipment card: asks the courier about this order's parcel right now and writes
    /// down what it said. The shop team asked on purpose, so the throttle that protects the
    /// customer-facing pull does not apply.
    ///
    /// This is also the only thing that moves an order for a parcel: when the courier's own status says
    /// the parcel is out (or delivered, or coming back), the order is moved along the steps its lifecycle
    /// allows - never past the end - and its units follow (see HC.Business.ShipmentStatusFlow). A courier
    /// that cannot be reached leaves the order exactly as it was, with the reason in <c>Messages</c>.
    /// </summary>
    public Task<OrderShipmentDto> TrackOrderShipmentAsync(long orderId, long currentUserId)
        => _shipmentTracking.RefreshAsync(orderId, currentUserId);
}
