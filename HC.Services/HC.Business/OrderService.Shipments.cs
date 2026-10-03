// ============================================================
// OrderService.Shipments.cs
// Partial class: OrderService - the parcel of an order, for the customer
// ============================================================

using HC.Business.Dtos;
using HC.Business.Shipping;

namespace HC.Business;

public partial class OrderService : IOrderService
{
    /// <summary>
    /// The throttled pull 'My Orders' makes when it opens and when the customer asks about a parcel:
    /// every live parcel of the signed-in customer's orders is looked up with the courier - the ones
    /// that were looked up recently, and the ones the courier is done with, are left alone - and the
    /// fresh snapshots come back for the page to show.
    ///
    /// The customer id is always the signed-in one (never one from the request), so this can only ever
    /// answer about the caller's own parcels. Nothing here throws: a courier that cannot be reached is a
    /// sentence in <c>Messages</c> and the page carries on (the same rule the PIN code lookup keeps).
    /// </summary>
    public Task<RefreshOrderShipmentsDto> RefreshShipmentsAsync(long customerId)
    {
        if (customerId <= 0)
        {
            return Task.FromResult(new RefreshOrderShipmentsDto
            {
                Result = 0,
                Messages = new[] { "Please sign in to track your parcels." }
            });
        }

        return _shipmentTracking.RefreshForCustomerAsync(customerId);
    }
}
