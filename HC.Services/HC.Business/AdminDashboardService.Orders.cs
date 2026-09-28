// ============================================================
// AdminDashboardService.Orders.cs
// Partial class: AdminDashboardService - Orders operations
// ============================================================

using System.Security.Cryptography;
using System.Text;
using HC.Business.Dtos;
using HC.Data;
using HC.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace HC.Business;

public partial class AdminDashboardService : IAdminDashboardService
{
    // Orders.OrderStatusID values - see HC.Data/Scripts/SeedOrderStatuses.sql.
    private const short OrderStatusPending = 1;
    private const short OrderStatusConfirmed = 2;
    private const short OrderStatusShipped = 3;
    private const short OrderStatusDelivered = 4;
    private const short OrderStatusCancelled = SkuAvailability.CancelledOrderStatusId; // 5

    // SKUStatuses.SKUStatusID values - see HC.Data/Scripts/SeedSkuStatuses.sql. 'Available' and
    // 'Ordered' come from SkuAvailability, so the shop front and the admin share one definition of
    // which pool a physical unit sits in.
    private const short SkuStatusAvailable = SkuAvailability.AvailableSkuStatusId; // 1
    private const short SkuStatusOrdered = SkuAvailability.OrderedSkuStatusId;     // 4
    private const short SkuStatusDispatched = 5;
    private const short SkuStatusDelivered = 7;

    /// <summary>
    /// The steps the shop team may take from each status - the same lifecycle the storefront uses
    /// (Pending -> Confirmed (paid) -> Shipped -> Delivered) plus cancellation. Delivered and
    /// Cancelled are terminal: a delivered order is a return (book it as a new purchase) and a
    /// cancelled order has already put its units back on the shelf.
    /// </summary>
    private static readonly Dictionary<short, short[]> OrderStatusTransitions = new()
    {
        [OrderStatusPending] = new[] { OrderStatusConfirmed, OrderStatusCancelled },
        [OrderStatusConfirmed] = new[] { OrderStatusShipped, OrderStatusCancelled },
        [OrderStatusShipped] = new[] { OrderStatusDelivered, OrderStatusCancelled },
        [OrderStatusDelivered] = Array.Empty<short>(),
        [OrderStatusCancelled] = Array.Empty<short>()
    };

    /// <summary>
    /// Every order for the admin list, newest first. <c>TotalAmount</c> is what the checkout charged
    /// (OrderItems holds one row per physical unit and the checkout charges the unit prices) and
    /// <c>IsPaid</c> mirrors 'My Orders': an order counts as paid once it is Confirmed, Shipped or
    /// Delivered - a Pending order has not been paid yet and a cancelled one is not money we keep.
    /// </summary>
    public async Task<List<AdminOrderListDto>> GetOrdersAsync()
    {
        return await _context.Orders
            .AsNoTracking()
            .OrderByDescending(o => o.OrderDate)
            .Select(o => new AdminOrderListDto
            {
                OrderId = o.OrderId,
                OrderNumber = "HC" + o.OrderId.ToString("D6"),
                OrderDate = o.OrderDate,
                CustomerName = (o.Customer.FirstName + " " + (o.Customer.LastName ?? "")).Trim(),
                StatusId = o.OrderStatusId,
                Status = o.OrderStatus.Status,
                IsPaid = o.OrderStatusId == OrderStatusConfirmed
                         || o.OrderStatusId == OrderStatusShipped
                         || o.OrderStatusId == OrderStatusDelivered,
                TotalAmount = o.OrderItems.Sum(oi => oi.UnitPrice),
                ItemCount = o.OrderItems.Count
            })
            .ToListAsync();
    }

    /// <summary>
    /// One order with its addresses, its physical units and its history, plus the statuses the shop
    /// team may move it to next. The status shown for every history step comes from the history row
    /// itself (OrderHistory carries its own OrderStatusID), so "Order placed" keeps showing Pending
    /// after the order has moved on - reading it from the order relabels the whole timeline.
    /// </summary>
    public async Task<AdminOrderDetailDto?> GetOrderDetailAsync(long orderId)
    {
        // The lifecycle steps come from the lookup table instead of hard-coded names, so a step added
        // by the shop team shows up here too.
        var statusNames = await _context.OrderStatuses
            .AsNoTracking()
            .ToDictionaryAsync(s => s.OrderStatusId, s => s.Status);

        var order = await _context.Orders
            .AsNoTracking()
            .Where(o => o.OrderId == orderId)
            .Select(o => new AdminOrderDetailDto
            {
                OrderId = o.OrderId,
                OrderNumber = "HC" + o.OrderId.ToString("D6"),
                OrderDate = o.OrderDate,
                CustomerName = (o.Customer.FirstName + " " + (o.Customer.LastName ?? "")).Trim(),
                CustomerEmail = o.Customer.EmailId,
                CustomerMobile = o.Customer.MobileNumber ?? "",
                StatusId = o.OrderStatusId,
                Status = o.OrderStatus.Status,
                IsPaid = o.OrderStatusId == OrderStatusConfirmed
                         || o.OrderStatusId == OrderStatusShipped
                         || o.OrderStatusId == OrderStatusDelivered,
                TotalAmount = o.OrderItems.Sum(oi => oi.UnitPrice),
                SellerName = o.Seller.PartnerName,
                BillingAddress = new AdminAddressDto
                {
                    AddressTitle = o.BillingAddress.AddressTitle,
                    ContactName = o.BillingAddress.ContactName,
                    AddressLine1 = o.BillingAddress.AddressLine1,
                    AddressLine2 = o.BillingAddress.AddressLine2,
                    City = o.BillingAddress.City,
                    State = o.BillingAddress.State,
                    Zipcode = o.BillingAddress.Zipcode,
                    MobileNumber = o.BillingAddress.MobileNumber
                },
                ShippingAddress = new AdminAddressDto
                {
                    AddressTitle = o.ShippingAddress.AddressTitle,
                    ContactName = o.ShippingAddress.ContactName,
                    AddressLine1 = o.ShippingAddress.AddressLine1,
                    AddressLine2 = o.ShippingAddress.AddressLine2,
                    City = o.ShippingAddress.City,
                    State = o.ShippingAddress.State,
                    Zipcode = o.ShippingAddress.Zipcode,
                    MobileNumber = o.ShippingAddress.MobileNumber
                },
                Items = o.OrderItems.Select(oi => new AdminOrderItemDto
                {
                    Sku = oi.Sku,
                    ProductName = oi.ProductName,
                    ProductTitle = oi.ProductTitle,
                    UnitPrice = oi.UnitPrice,
                    DiscountPercent = oi.DiscountPercent,
                    AdditionalDiscountPercent = oi.AdditionalDiscountPercent,
                    DeliveryCharge = oi.DeliveryCharge,
                    PackagingCharge = oi.PackagingCharge,
                    StorageCharge = oi.StorageCharge,
                    ProfitMarginPercent = oi.ProfitMarginPercent,
                    Cgstpercent = oi.Cgstpercent,
                    Sgstpercent = oi.Sgstpercent,
                    Igstpercent = oi.Igstpercent
                }).ToList(),
                History = o.OrderHistories
                    .OrderBy(h => h.HistoryDate)
                    .Select(h => new AdminOrderHistoryDto
                    {
                        HistoryDate = h.HistoryDate,
                        StatusId = h.OrderStatusId,
                        Comments = h.Comments ?? ""
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync();

        if (order == null)
            return null;

        foreach (var step in order.History)
        {
            step.Status = statusNames.GetValueOrDefault(step.StatusId, "");
        }

        order.AvailableStatuses = (OrderStatusTransitions.GetValueOrDefault(order.StatusId) ?? Array.Empty<short>())
            .Select(statusId => new AdminOrderStatusDto
            {
                StatusId = statusId,
                Status = statusNames.GetValueOrDefault(statusId, "")
            })
            .ToList();

        return order;
    }

    /// <summary>The order lifecycle steps, for the status filter of the admin order list.</summary>
    public async Task<List<AdminOrderStatusDto>> GetOrderStatusesAsync()
    {
        return await _context.OrderStatuses
            .AsNoTracking()
            .OrderBy(s => s.OrderStatusId)
            .Select(s => new AdminOrderStatusDto
            {
                StatusId = s.OrderStatusId,
                Status = s.Status
            })
            .ToListAsync();
    }

    /// <summary>
    /// Moves an order to the next step of its lifecycle from the admin order screen and keeps the
    /// stock in step with it: dispatching marks the units 'Dispatched', delivering marks them
    /// 'Delivered' and cancelling puts the units the shop still holds back into the Available pool
    /// (exactly like the customer's own cancellation), so they can be sold again. The new step is
    /// written to OrderHistory together with the admin's note - that is what the customer sees in
    /// 'My Orders'.
    /// </summary>
    public async Task<AdminResultDto> UpdateOrderStatusAsync(long orderId, AdminOrderStatusUpdateRequest request, long currentUserId)
    {
        if (request.StatusId <= 0)
            return Error("Select the status to move this order to.");

        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var order = await _context.Orders
                .Include(o => o.OrderStatus)
                .Include(o => o.OrderItems)
                .FirstOrDefaultAsync(o => o.OrderId == orderId);

            if (order == null)
                return Error("Order not found.");

            var currentStatusName = order.OrderStatus?.Status ?? $"status {order.OrderStatusId}";

            if (order.OrderStatusId == request.StatusId)
                return Error($"{OrderNumber(order.OrderId)} is already {currentStatusName}.");

            var newStatusName = await _context.OrderStatuses
                .Where(s => s.OrderStatusId == request.StatusId)
                .Select(s => s.Status)
                .FirstOrDefaultAsync();

            if (newStatusName == null)
                return Error("Invalid order status.");

            var allowedStatuses = OrderStatusTransitions.GetValueOrDefault(order.OrderStatusId) ?? Array.Empty<short>();
            if (!allowedStatuses.Contains(request.StatusId))
            {
                var nextSteps = allowedStatuses.Length == 0
                    ? $"{currentStatusName} is the end of the line for this order."
                    : $"The next step is {string.Join(" or ", await StatusNamesAsync(allowedStatuses))}.";

                return Error($"An order that is {currentStatusName} cannot be moved to {newStatusName}. {nextSteps}");
            }

            var comment = CleanOptional(request.Comments);
            if (request.StatusId == OrderStatusCancelled && comment == null)
            {
                return Error(
                    "Please say why the order is cancelled - the note is kept in the order history and " +
                    "shown to the customer.");
            }

            var now = DateTime.UtcNow;
            var updatedUnits = await MoveOrderUnitsAsync(order, request.StatusId, now);

            order.OrderStatusId = request.StatusId;

            var historyComment = comment ?? DefaultStatusComment(request.StatusId, newStatusName);
            var loginId = await GetAdminLoginIdAsync(currentUserId);

            _context.OrderHistories.Add(new OrderHistory
            {
                OrderId = order.OrderId,
                HistoryDate = now,
                OrderStatusId = request.StatusId,
                Comments = loginId == null ? historyComment : $"{historyComment} (by {loginId})"
            });

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            var unitNote = updatedUnits == 1 ? " 1 unit updated." : $" {updatedUnits} units updated.";

            return new AdminResultDto
            {
                Result = 1,
                Messages = new[]
                {
                    $"{OrderNumber(order.OrderId)} is now {newStatusName}." + (updatedUnits > 0 ? unitNote : "")
                }
            };
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            _context.ChangeTracker.Clear();

            return Error("The order status could not be changed. Nothing was saved - please try again.");
        }
    }

    /// <summary>The lifecycle step names for the given status ids, in the lookup table's order.</summary>
    private async Task<List<string>> StatusNamesAsync(IEnumerable<short> statusIds)
    {
        var ids = statusIds.ToList();

        return await _context.OrderStatuses
            .Where(s => ids.Contains(s.OrderStatusId))
            .OrderBy(s => s.OrderStatusId)
            .Select(s => s.Status)
            .ToListAsync();
    }

    /// <summary>
    /// Follows the order to its physical units: cancelling gives back the units the shop still holds
    /// (a cancelled order that had already left the shop still puts them back on the shelf),
    /// dispatching and delivering move them one step further. Every change is written to SKUHistory,
    /// exactly like the storefront's checkout and cancellation do, so the unit's story stays complete.
    /// Returns how many units were touched.
    /// </summary>
    private async Task<int> MoveOrderUnitsAsync(Order order, short newOrderStatusId, DateTime now)
    {
        var (fromStatuses, toStatus) = newOrderStatusId switch
        {
            OrderStatusShipped => (new short[] { SkuStatusOrdered }, SkuStatusDispatched),
            OrderStatusDelivered => (new short[] { SkuStatusOrdered, SkuStatusDispatched }, SkuStatusDelivered),
            OrderStatusCancelled => (new short[] { SkuStatusOrdered, SkuStatusDispatched }, SkuStatusAvailable),

            // Confirming an order (the money arrived) leaves its units reserved as 'Ordered'.
            _ => (Array.Empty<short>(), (short)0)
        };

        if (fromStatuses.Length == 0)
            return 0;

        var updated = 0;

        foreach (var item in order.OrderItems)
        {
            var sku = await _context.Skus.FirstOrDefaultAsync(s => s.Sku1 == item.Sku);
            if (sku == null || !fromStatuses.Contains(sku.SkustatusId))
                continue;

            sku.SkustatusId = toStatus;

            _context.Skuhistories.Add(new Skuhistory
            {
                Sku = sku.Sku1,
                InventoryId = sku.InventoryId,
                SkustatusId = toStatus,
                HistoryDate = now
            });

            updated++;
        }

        return updated;
    }

    /// <summary>The login id of the admin making the change, so the order history says who did it.</summary>
    private async Task<string?> GetAdminLoginIdAsync(long userId)
    {
        if (userId <= 0)
            return null;

        return await _context.Users
            .Where(u => u.UserId == userId)
            .Select(u => u.LoginId)
            .FirstOrDefaultAsync();
    }

    /// <summary>What the order history records when the admin does not type a note of their own.</summary>
    private static string DefaultStatusComment(short statusId, string statusName) => statusId switch
    {
        OrderStatusConfirmed => "Payment received - order confirmed by the shop team",
        OrderStatusShipped => "Order dispatched",
        OrderStatusDelivered => "Order delivered",
        _ => $"Order moved to {statusName} by the shop team"
    };

    private static string OrderNumber(long orderId) => $"HC{orderId:D6}";

}
