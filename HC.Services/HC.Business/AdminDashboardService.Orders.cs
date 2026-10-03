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
    // Orders.OrderStatusID values - see HC.Data/Scripts/SeedOrderStatuses.sql. The lifecycle itself
    // (which status may follow which) lives in OrderStatusFlow, which the storefront reads as well, so
    // the steps offered here and the customer's own cancel option cannot drift apart.
    private const short OrderStatusPending = OrderStatusFlow.Pending;
    private const short OrderStatusConfirmed = OrderStatusFlow.Confirmed;
    private const short OrderStatusShipped = OrderStatusFlow.Shipped;
    private const short OrderStatusDelivered = OrderStatusFlow.Delivered;
    private const short OrderStatusCancelled = OrderStatusFlow.Cancelled; // 5

    // SKUStatuses.SKUStatusID values and the pools a unit moves between live in SkuAvailability, so the
    // shop front, the admin screen and the tracking pull share one definition of where a unit is.

    /// <summary>
    /// Every order for the admin list, newest first. <c>TotalAmount</c> is what the checkout charged
    /// (OrderItems holds one row per physical unit and the checkout charges the unit prices) and
    /// <c>IsPaid</c> mirrors 'My Orders': an order counts as paid once it is Confirmed, Shipped or
    /// Delivered - a Pending order has not been paid yet and a cancelled one is not money we keep.
    /// Every row also carries the customer's id and email, so a customer can be identified by more
    /// than a name.
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
                CustomerId = o.CustomerId,
                CustomerEmail = o.Customer.EmailId,
                StatusId = o.OrderStatusId,
                Status = o.OrderStatus.Status,
                IsPaid = o.OrderStatusId == OrderStatusConfirmed
                         || o.OrderStatusId == OrderStatusShipped
                         || o.OrderStatusId == OrderStatusDelivered,
                TotalAmount = o.OrderItems.Sum(oi => oi.UnitPrice),
                ItemCount = o.OrderItems.Count,

                // A refund is owed on an order whose money we still hold: the customer cancelled a paid
                // order (RefundRequested), a refund Razorpay refused (RefundFailed, retried from the
                // order screen), or a cancelled order whose capture was never given back - an order
                // cancelled by the shop team is refunded straight away, so it never shows up here.
                RefundPending = o.OrderPayments.Any(p =>
                    p.Status == OrderPaymentStatus.RefundRequested ||
                    p.Status == OrderPaymentStatus.RefundFailed ||
                    (o.OrderStatusId == OrderStatusCancelled && p.Status == OrderPaymentStatus.Captured))
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
                CustomerId = o.CustomerId,
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

        // The money side of the order: its newest payment row carries the refund state, and the row
        // itself is what a refund is sent against. Read on its own because no other screen needs the
        // whole list of attempts.
        var payment = await _context.OrderPayments
            .AsNoTracking()
            .Where(p => p.OrderId == orderId)
            .OrderByDescending(p => p.PaymentId)
            .FirstOrDefaultAsync();

        if (payment != null)
        {
            order.RefundPending = payment.Status == OrderPaymentStatus.RefundRequested
                                  || payment.Status == OrderPaymentStatus.RefundFailed
                                  || (order.StatusId == OrderStatusCancelled &&
                                      payment.Status == OrderPaymentStatus.Captured);

            order.RefundRequestedOn = payment.RefundRequestedOn;
            order.RefundRequestedComment = payment.RefundRequestedComment;

            // What is owed back: the amount the gateway took, or the order total when that was never
            // recorded (a request written without a capture behind it).
            order.RefundAmount = payment.AmountInPaise > 0
                ? payment.AmountInPaise / 100m
                : order.TotalAmount;

            order.RefundId = payment.RefundId;
            order.RefundStatus = payment.RefundStatus;
            order.RefundedOn = payment.RefundedOn;
            order.RefundFailureReason = payment.RefundFailureReason;
            order.RazorpayPaymentId = payment.RazorpayPaymentId;
        }

        foreach (var step in order.History)
        {
            step.Status = statusNames.GetValueOrDefault(step.StatusId, "");
        }

        // The parcel, if the shop has recorded one. Read on its own because it lives in its own table and
        // comes through the provider-neutral tracking service - which is also what keeps opening an order
        // off the courier's critical path: this only reads what was written down last time.
        order.Shipment = await _shipmentTracking.GetForOrderAsync(orderId);

        // The steps that follow the status the order is in - the same chain the storefront reads, so an
        // order that has been shipped cannot be sent back and a cancelled one is the end of the line.
        order.AvailableStatuses = OrderStatusFlow.NextFrom(order.StatusId)
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
    ///
    /// Shipped is also when the parcel is booked, when there is one: the provider the team picked, the
    /// consignment number that provider gave it - or, for a provider with no panel behind it (the shop's
    /// own delivery service), the reference the system mints for the parcel - and what the courier billed
    /// for it (optional - the books' own figure, never shown to the customer) are recorded in the same
    /// move, through <see cref="ShipmentTrackingService"/> (the one writer of OrderShipments), so every
    /// courier call later - the customer's pull and the screen's 'Track now' - goes through that provider's
    /// own adapter, because the adapter is looked up from the parcel itself. An order dispatched with no
    /// consignment number and a provider that mints none has no parcel at all, and simply says Shipped; a
    /// provider this shop is not set up with, or a request the provider would not accept, rolls the whole
    /// move back: nothing is saved and the reason is answered.
    ///
    /// Cancelling a paid order also gives the money back, straight away (see RazorpayRefunds). A
    /// refusal from the gateway does NOT undo the cancellation: the order is cancelled and the money
    /// stays marked as owed on the payment row, so it shows up as a refund due and can be approved
    /// again - from a server whose keys can see the payment - or refunded in the Razorpay dashboard
    /// and closed with 'Mark refunded'.
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

            // Only a step the chain allows is accepted (Pending -> Confirmed -> Shipped -> Delivered,
            // with cancellation from any of the live statuses), so an order can never be moved backwards
            // or jump straight from Confirmed to Delivered.
            var allowedStatuses = OrderStatusFlow.NextFrom(order.OrderStatusId);
            if (!allowedStatuses.Contains(request.StatusId))
            {
                var nextSteps = allowedStatuses.Length == 0
                    ? $"{currentStatusName} is the end of the line for this order."
                    : $"The next step is {string.Join(" or ", await StatusNamesAsync(allowedStatuses))}.";

                return Error($"An order that is {currentStatusName} cannot be moved to {newStatusName}. {nextSteps}");
            }

            // Shipping is the step that asks about the parcel, but nothing about it is required: an order
            // that went out without a courier - handed over in person, or given to a delivery service
            // this shop has no integration with - is simply dispatched, with no parcel recorded and
            // nothing to track. One can still be recorded later from the Shipment card.
            //
            // It is the provider's own answer that makes this a parcel, not the status and not this
            // method: a consignment number the team typed is one, and so is a blank number against a
            // provider that gives the parcel a reference of its own (the shop's own delivery service - see
            // IShipmentTrackingService.RecordsParcel). Asking it here is what keeps this service from
            // having to know which provider mints what.
            var consignmentNumber = (request.AwbNumber ?? string.Empty).Trim();

            var parcelRequest = new SaveOrderShipmentRequest
            {
                Provider = request.Provider,
                AwbNumber = consignmentNumber,
                CourierName = request.CourierName,
                FreightCharge = request.FreightCharge
            };

            var recordsParcel = request.StatusId == OrderStatusShipped &&
                                _shipmentTracking.RecordsParcel(parcelRequest);

            // What the courier billed is kept on the parcel it was billed for, so a figure with no parcel
            // to live on has nowhere to go. Refused rather than quietly dropped: a charge the team typed
            // in and the books never got is the kind of loss nobody notices until the month is closed.
            if (request.StatusId == OrderStatusShipped && !recordsParcel && request.FreightCharge is not null)
            {
                return Error(
                    "A freight charge is kept on the parcel it was billed for, so give this one a " +
                    "consignment number (AWB), or pick the provider that carries it without one - the " +
                    "shop's own service, which gives the parcel a reference itself. If nothing was billed " +
                    "for it, leave the freight blank.");
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

            var history = new OrderHistory
            {
                OrderId = order.OrderId,
                HistoryDate = now,
                OrderStatusId = request.StatusId,
                Comments = loginId == null ? historyComment : $"{historyComment} (by {loginId})"
            };

            _context.OrderHistories.Add(history);

            var parcelNote = string.Empty;

            // Shipping records the parcel in the same move, when there is one to record: the provider it
            // was booked with, the consignment number it gave the team (or, for a provider that gives none,
            // the reference the tracking service mints for the parcel) and what it billed for it are written
            // through the one service that owns OrderShipments (ShipmentTrackingService), against the status
            // the order has just been given - so the timeline shows the parcel as a Shipped step, and every
            // later courier call is made through that provider's own adapter, because the tracking service
            // resolves the adapter from the parcel itself (OrderShipments.Provider) and never from a
            // hard-coded one. A provider this shop is not set up with, or a consignment the provider would
            // not accept, rolls the whole move back: the order is left exactly as it was and the reason is
            // answered. A Shipped move with no parcel at all skips this altogether - there is nothing about
            // the delivery for this side to remember.
            if (recordsParcel)
            {
                var parcel = await _shipmentTracking.SaveAsync(order.OrderId, parcelRequest, currentUserId);

                if (parcel.Result != 1)
                {
                    await transaction.RollbackAsync();
                    _context.ChangeTracker.Clear();

                    return Error(parcel.Messages.FirstOrDefault() ??
                        "The parcel could not be recorded, so the order was not shipped. Nothing was saved - please try again.");
                }

                parcelNote = " " + string.Join(" ", parcel.Messages);
            }

            // A parcel the shop carries itself is brought level with this very move, inside the same
            // transaction: there is no courier behind it to report anything, so the move just made is what its
            // card and 'My Orders' show - without this, both would go on saying "our own delivery arrangement"
            // for a parcel that has already arrived, and never the day it did. A courier-carried parcel is left
            // alone here (its provider is asked, never assumed), and a move with nothing to do with a parcel
            // mirrors nothing at all, so this usually answers with "" (see
            // IShipmentTrackingService.MirrorOrderStatusAsync).
            var ownDeliveryNote = await _shipmentTracking.MirrorOrderStatusAsync(order.OrderId, request.StatusId, now);
            var ownDeliveryText = ownDeliveryNote.Length > 0 ? " " + ownDeliveryNote : string.Empty;

            await _context.SaveChangesAsync();

            var unitNote = updatedUnits == 1 ? " 1 unit updated." : $" {updatedUnits} units updated.";

            // Cancelling a paid order means giving the money back. The refund runs last, just before the
            // commit, because it is the one step that cannot be undone once Razorpay has accepted it. The
            // cancellation itself is never given up over a refund, though: an order the shop team cancelled
            // stays cancelled and the money it still holds stays flagged as owed - the payment row reads
            // RefundFailed with the gateway's own wording as the reason, which is what the Refund card shows
            // ("Refund due", plus why the last attempt did not go through), and 'Approve refund' can be
            // pressed again from a server whose keys can see the payment. Rolling the cancellation back
            // instead would leave the order live and the money owed - the worst of both.
            var refundNote = string.Empty;

            if (request.StatusId == OrderStatusCancelled)
            {
                var (refund, refundMessage) = await RazorpayRefunds.RefundOrderPaymentAsync(
                    _context, _razorpayKeyId, _razorpayKeySecret, order, comment ?? "Order cancelled");

                if (refund == PaymentRefundResult.Failed)
                {
                    // The refusal is already on the payment row (written by RazorpayRefunds, and kept by
                    // the commit below). The shop team gets Razorpay's own wording in the answer - the
                    // customer-facing history only says the money is being arranged.
                    history.Comments += $" {RefundStillOwedNote(order.OrderId)}";

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return new AdminResultDto
                    {
                        Result = 1,
                        Messages = new[]
                        {
                            $"{OrderNumber(order.OrderId)} is now {newStatusName}." +
                            (updatedUnits > 0 ? unitNote : string.Empty) +
                            parcelNote +
                            ownDeliveryText +
                            $" {refundMessage}"
                        }
                    };
                }

                if (refund != PaymentRefundResult.NothingToRefund)
                {
                    // The shop team is answered with Razorpay's own wording; the order history - which
                    // the customer reads in 'My Orders' - gets the plain note instead.
                    refundNote = $" {refundMessage}";
                    history.Comments += $" {RefundSentNote(order.OrderId)}";

                    await _context.SaveChangesAsync();
                }
            }

            await transaction.CommitAsync();

            return new AdminResultDto
            {
                Result = 1,
                Messages = new[]
                {
                    $"{OrderNumber(order.OrderId)} is now {newStatusName}." +
                    (updatedUnits > 0 ? unitNote : string.Empty) +
                    parcelNote +
                    ownDeliveryText +
                    refundNote
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

    /// <summary>
    /// Approves the refund a cancelled paid order is waiting for. This is the step that actually gives
    /// the money back: the refund is sent to Razorpay against the payment on record (see
    /// RazorpayRefunds) and the order history records that it went out, and who approved it, so the
    /// customer reads it in 'My Orders' and the shop team finds it in the order's history.
    ///
    /// Only an order whose payment still says the money is owed can be approved - a refund already
    /// recorded, or an order that was never paid, is turned away - so approving twice can never refund
    /// twice. A refusal from Razorpay is written on the payment row (and shown to the shop team on this
    /// screen); the history only says the money is still owed, because that text is read by the customer -
    /// the gateway's own wording is never put in front of them. The shop team either approves it again or
    /// refunds it in the Razorpay dashboard and records that with 'Mark refunded'.
    /// </summary>
    public async Task<AdminResultDto> ApproveOrderRefundAsync(long orderId, long currentUserId)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .FirstOrDefaultAsync(o => o.OrderId == orderId);

            if (order == null)
                return Error("Order not found.");

            var payment = await FindRefundPaymentAsync(order);
            if (payment == null)
                return Error(NoRefundWaitingMessage(orderId));

            var (refund, refundMessage) = await RazorpayRefunds.RefundOrderPaymentAsync(
                _context,
                _razorpayKeyId,
                _razorpayKeySecret,
                order,
                payment.RefundRequestedComment ?? "Order cancelled");

            var now = DateTime.UtcNow;

            if (refund == PaymentRefundResult.Failed)
            {
                // The refusal is already on the payment row (RefundFailureReason, which is what the Refund
                // card shows the shop team) and the answer below carries Razorpay's own wording; the
                // customer only reads that the money is still owed.
                await AddRefundHistoryAsync(order, now, RefundStillOwedNote(order.OrderId), currentUserId);
                await transaction.CommitAsync();

                return Error(refundMessage);
            }

            if (refund == PaymentRefundResult.NothingToRefund)
            {
                // Money we are holding but cannot send back ourselves (paid before payments were
                // recorded here): nothing is changed, the shop team refunds it in the Razorpay
                // dashboard and records that with 'Mark refunded'.
                await transaction.RollbackAsync();
                _context.ChangeTracker.Clear();

                return Error(refundMessage);
            }

            // The shop team is told the refund id and status in the answer; the customer's history only
            // says the money is on its way back.
            await AddRefundHistoryAsync(order, now, RefundSentNote(order.OrderId), currentUserId);
            await transaction.CommitAsync();

            return new AdminResultDto
            {
                Result = 1,
                Messages = new[] { refundMessage }
            };
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            _context.ChangeTracker.Clear();

            return Error("The refund could not be approved. Nothing was changed - please try again.");
        }
    }

    /// <summary>
    /// Records that the shop team gave the money back outside the app (in the Razorpay dashboard), so
    /// an outstanding refund stops being counted as owed: the payment row is marked Refunded with the
    /// amount and the note, and the history says who recorded it. This is how the refunds the app
    /// cannot send itself are closed - an order paid before payments were recorded here has no Razorpay
    /// payment id to refund against - and how a refund made by hand after a refusal is tidied up.
    /// </summary>
    public async Task<AdminResultDto> MarkOrderRefundedAsync(long orderId, string? comment, long currentUserId)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .FirstOrDefaultAsync(o => o.OrderId == orderId);

            if (order == null)
                return Error("Order not found.");

            var payment = await FindRefundPaymentAsync(order);
            if (payment == null)
                return Error(NoRefundWaitingMessage(orderId));

            var note = CleanOptional(comment)
                       ?? payment.RefundRequestedComment
                       ?? "Refunded by the shop team in Razorpay";

            var (recorded, message) = await RazorpayRefunds.RecordManualRefundAsync(_context, order, note);

            if (!recorded)
            {
                await transaction.RollbackAsync();
                _context.ChangeTracker.Clear();

                return Error(message);
            }

            // The customer reads the plain note that the shop team gave the money back, plus whatever they
            // typed; Razorpay's refund id stays on the payment row and on the admin order screen.
            await AddRefundHistoryAsync(
                order, DateTime.UtcNow, $"{RefundRecordedByTeamNote(order.OrderId)} {note}", currentUserId);
            await transaction.CommitAsync();

            return new AdminResultDto
            {
                Result = 1,
                Messages = new[] { message }
            };
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            _context.ChangeTracker.Clear();

            return Error("The refund could not be recorded. Nothing was changed - please try again.");
        }
    }

    /// <summary>
    /// The payment of <paramref name="order"/> that a refund may be approved for: the newest row whose
    /// status still says the money is owed - a RefundRequested (the customer cancelled a paid order), a
    /// RefundFailed (Razorpay refused it, so it is retried from here), or the Captured row of an order
    /// that was cancelled before its refund was ever recorded. Null when there is nothing left to give
    /// back, which is what stops a second approval from refunding twice.
    /// </summary>
    private async Task<OrderPayment?> FindRefundPaymentAsync(Order order)
    {
        var payments = await _context.OrderPayments
            .Where(p => p.OrderId == order.OrderId)
            .OrderByDescending(p => p.PaymentId)
            .ToListAsync();

        if (payments.Any(p => p.Status == OrderPaymentStatus.Refunded || !string.IsNullOrEmpty(p.RefundId)))
            return null;

        return payments.FirstOrDefault(p => p.Status == OrderPaymentStatus.RefundRequested)
            ?? payments.FirstOrDefault(p => p.Status == OrderPaymentStatus.RefundFailed)
            ?? (order.OrderStatusId == OrderStatusCancelled
                ? payments.FirstOrDefault(p => p.Status == OrderPaymentStatus.Captured)
                : null);
    }

    /// <summary>
    /// What 'My Orders' says when a refund has gone out: the money is on its way back to the customer's
    /// own payment method. Deliberately plain - the Razorpay refund id and status belong to the shop team
    /// (they are on the payment row and on the admin order screen), never on the customer's timeline.
    /// </summary>
    private static string RefundSentNote(long orderId) =>
        $"The refund of {OrderNumber(orderId)} has been sent back to the payment method you used - it " +
        "usually reaches you within 5-7 working days.";

    /// <summary>
    /// What 'My Orders' says when the gateway refused the refund: the money is still owed and the shop team
    /// is arranging it. Razorpay's error text ('Razorpay returned 400 Bad Request: ...') is kept for the shop
    /// team - in the payment row's RefundFailureReason and in the answer they get when they approve - and is
    /// never written into the customer-facing history.
    /// </summary>
    private static string RefundStillOwedNote(long orderId) =>
        $"The refund of {OrderNumber(orderId)} is being arranged by our team - the money goes back to the " +
        "payment method you used, usually within 5-7 working days.";

    /// <summary>
    /// What 'My Orders' says about a refund the shop team made outside the app and recorded here.
    /// </summary>
    private static string RefundRecordedByTeamNote(long orderId) =>
        $"The refund of {OrderNumber(orderId)} was made by our team - the money goes back to the payment " +
        "method you used.";

    /// <summary>
    /// Writes what happened to the money into the order history - a refund is part of the order's story
    /// and 'My Orders' shows it to the customer. The step is filed under the status the order is in
    /// (Cancelled), with the admin who did it named when their login is known. The note is always the plain
    /// one built by the callers (RefundSentNote / RefundStillOwedNote / RefundRecordedByTeamNote): this text
    /// is read by customers, so Razorpay's own wording stays on the payment row and in the shop team's
    /// answer instead.
    /// </summary>
    private async Task AddRefundHistoryAsync(Order order, DateTime now, string message, long currentUserId)
    {
        var loginId = await GetAdminLoginIdAsync(currentUserId);
        var comment = loginId == null ? message : $"{message} (by {loginId})";

        // A retry that ends the same way as the attempt before it adds nothing the customer needs to read
        // twice: 'Approve refund' can be pressed again whenever the shop team likes, and every refusal
        // would otherwise put the same sentence on the customer's timeline. The latest refusal is always on
        // the payment row for the shop team.
        var lastComment = await _context.OrderHistories
            .Where(h => h.OrderId == order.OrderId)
            .OrderByDescending(h => h.HistoryId)
            .Select(h => h.Comments)
            .FirstOrDefaultAsync();

        if (lastComment == comment)
            return;

        _context.OrderHistories.Add(new OrderHistory
        {
            OrderId = order.OrderId,
            HistoryDate = now,
            OrderStatusId = order.OrderStatusId,
            Comments = comment
        });

        await _context.SaveChangesAsync();
    }

    /// <summary>What the shop team is told when an order has no refund left to approve or record.</summary>
    private static string NoRefundWaitingMessage(long orderId) =>
        $"{OrderNumber(orderId)} has no refund waiting: it was either never paid, or its refund is already recorded.";

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
        // The pools and the move itself live in SkuAvailability, next to the rule that decides whether a
        // unit can be sold - the tracking pull moves units through the same call, so the two can never
        // describe the stock differently.
        return await SkuAvailability.MoveUnitsForOrderStatusAsync(_context, order, newOrderStatusId, now);
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
