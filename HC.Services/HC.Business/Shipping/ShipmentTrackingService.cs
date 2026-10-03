using HC.Business.Dtos;
using HC.Data;
using HC.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HC.Business.Shipping;

/// <summary>
/// The parcel side of an order, for both audiences - the one place that reads and writes
/// <c>OrderShipments</c>.
///
/// It knows nothing about any single courier: the lookup goes through
/// <see cref="IShipmentProviderRegistry"/>, which hands back the adapter the parcel was booked with, and
/// the answer is read with HC.Business.ShipmentStatusFlow - the shop's own wording for where a parcel
/// is, and the only order status a courier status may move the order to (see
/// <see cref="ShipmentStatusFlow.OrderStatusFor"/>). Any adapter therefore drives the order and the stock
/// the same way, and a new one changes nothing here.
///
/// A parcel carried by a service the shop arranges itself has no courier behind it at all: its provider
/// says so (<see cref="IShipmentProvider.ReportsTracking"/>), so the pull never asks about it and both
/// screens offer no tracking for it - its order is moved along by the shop team's own status moves, and
/// those moves are written on the parcel as they happen (<see cref="MirrorOrderStatusAsync"/>), so it says
/// where it has got to without anyone being asked. The reference written on such a parcel is the one minted
/// for it here.
///
/// The pull is the only thing that advances an order by itself, and it does so under the order's own
/// lifecycle rules (<see cref="OrderStatusFlow.CanMove"/>: a parcel collected makes a Confirmed order
/// Shipped, a delivery makes a Shipped order Delivered, and an unpaid, delivered or cancelled order is
/// left exactly where it is), moving the units with it through
/// <see cref="SkuAvailability.MoveUnitsForOrderStatusAsync"/> - so a courier scan can never leave the
/// stock counters describing a different order than the screen does.
///
/// Nothing here throws at the caller: a courier that cannot be reached, a parcel without an AWB or an
/// order that is not found all come back as an answer with <c>Result</c> 0 and a sentence for the person
/// waiting (the same rule <see cref="IPincodeService"/> follows).
/// </summary>
public class ShipmentTrackingService : IShipmentTrackingService
{
    /// <summary>The minutes a parcel is left alone between two courier lookups (0 = every page open).</summary>
    public const string SyncThrottleKey = "Shipping:SyncThrottleMinutes";

    /// <summary>15 minutes: long enough that a page open is not a courier call, short enough to be current.</summary>
    private const int DefaultSyncThrottleMinutes = 15;

    // The column lengths the answers are written into (see HomecutiesDbContext). A longer answer is cut
    // to fit rather than thrown away by the database, because a mouthy provider must not cost the pull.
    private const int AwbMaxLength = 50;
    private const int CourierMaxLength = 100;
    private const int ProviderStatusMaxLength = 100;
    private const int LastStatusTextMaxLength = 500;
    private const int TrackingUrlMaxLength = 500;

    /// <summary>
    /// How long a provider's id may be, because that is what <c>OrderShipments.Provider</c> holds (see
    /// HomecutiesDbContext). A parcel is tied back to the adapter that booked it by this id, so an
    /// adapter whose <see cref="IShipmentProvider.Name"/> is longer would be cut down when the parcel is
    /// recorded and would no longer be found again - tracking would quietly fall back to the default
    /// provider, asking the wrong aggregator about the AWB. The startup log therefore reports any
    /// registered adapter whose name does not fit (see HC.Services/Program.cs) rather than leaving it to
    /// be discovered by a customer. Public so that check reads the limit from the one place that trims.
    /// </summary>
    public const int ProviderMaxLength = 20;

    private readonly HomecutiesDbContext _context;
    private readonly IShipmentProviderRegistry _providers;
    private readonly ILogger<ShipmentTrackingService> _logger;
    private readonly TimeSpan _syncThrottle;

    public ShipmentTrackingService(
        HomecutiesDbContext context,
        IShipmentProviderRegistry providers,
        IConfiguration configuration,
        ILogger<ShipmentTrackingService> logger)
    {
        _context = context;
        _providers = providers;
        _logger = logger;

        var minutes = int.TryParse(configuration[SyncThrottleKey], out var configured) && configured >= 0
            ? configured
            : DefaultSyncThrottleMinutes;
        _syncThrottle = TimeSpan.FromMinutes(minutes);
    }

    /// <summary>
    /// The shipping providers this shop is set up with, in the order they were registered and with the
    /// default marked - what the admin order screen offers when the shop team records a parcel. A
    /// provider that is not <c>Configured</c> is still listed (the parcel can be recorded), it simply
    /// cannot be tracked until its credentials are set.
    /// </summary>
    public IReadOnlyList<ShipmentProviderInfo> GetProviders() => _providers.Describe();

    /// <summary>
    /// The parcel of an order as it was last written down, or null when no parcel has been recorded for
    /// it. This never calls the courier: it is what the screens read, and only the pull refreshes it.
    ///
    /// A parcel is read by leg: the one that went out unless <paramref name="direction"/> asks for the
    /// other one (see <c>OrderShipment.DirectionForward/DirectionReverse</c>), which is what the admin's
    /// Return card does when it shows the pickup coming back.
    ///
    /// A read that fails is answered with null, not with an exception: the parcel is an extra on the
    /// order screen, so a table that cannot be read (an unfinished deployment, say) has to cost that
    /// screen its parcel - never the order the shop team is looking at.
    /// </summary>
    public async Task<OrderShipmentDto?> GetForOrderAsync(
        long orderId,
        string? direction = null,
        CancellationToken cancellationToken = default)
    {
        // Which leg is being read: the parcel that went out unless the caller asked for the other one -
        // only the admin's Return card reads a reverse parcel.
        var leg = OrderShipment.NormaliseDirection(direction);

        try
        {
            var shipment = await _context.OrderShipments
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.OrderId == orderId && s.Direction == leg, cancellationToken);

            return shipment == null ? null : ToDto(shipment);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The page that asked is gone - not this service's answer to give (see PincodeService).
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Reading the parcel of order {OrderId} failed.", orderId);

            return null;
        }
    }

    /// <summary>
    /// The parcels of the given orders, keyed by order id, for the screens that show a list ('My Orders'
    /// and the admin order detail): one query for the whole page rather than one per order.
    ///
    /// What the courier billed for a parcel is stripped from the answer (see ForCustomer): a list read is
    /// a customer-facing read. So is the leg: a list shows the parcel the customer is waiting for (the
    /// forward one) - a return's own leg belongs to the Return card of the admin order screen.
    ///
    /// Answered with an empty map when the read fails, for the reason <see cref="GetForOrderAsync"/>
    /// gives: the orders on the page are the screen, the parcels are the extra.
    /// </summary>
    public async Task<Dictionary<long, OrderShipmentDto>> GetForOrdersAsync(
        IEnumerable<long> orderIds, CancellationToken cancellationToken = default)
    {
        var ids = orderIds.Distinct().ToList();

        if (ids.Count == 0)
            return new Dictionary<long, OrderShipmentDto>();

        try
        {
            var shipments = await _context.OrderShipments
                .AsNoTracking()
                .Where(s => ids.Contains(s.OrderId) && s.Direction == OrderShipment.DirectionForward)
                .ToListAsync(cancellationToken);

            return shipments.ToDictionary(s => s.OrderId, s => ForCustomer(ToDto(s)));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The page that asked is gone - not this service's answer to give (see PincodeService).
            throw;
        }
        catch (Exception exception)
        {
            // Belt and braces: the parcels are what 'My Orders' shows beside the order, not the order
            // itself, so a read that fails leaves those orders without a parcel rather than the customer
            // without their history.
            _logger.LogWarning(exception, "Reading the parcels of {OrderCount} orders failed.", ids.Count);

            return new Dictionary<long, OrderShipmentDto>();
        }
    }

    /// <summary>
    /// The parcels coming back, keyed by order id, for the screens that show a list: the return's own counterpart
    /// of <see cref="GetForOrdersAsync"/>, one query for the whole page. An order with no return parcel is simply
    /// absent from the answer, and what the courier billed is stripped, exactly as it is for the forward leg -
    /// this read is the customer's own page.
    /// </summary>
    public async Task<Dictionary<long, OrderShipmentDto>> GetReverseForOrdersAsync(
        IEnumerable<long> orderIds, CancellationToken cancellationToken = default)
    {
        var ids = orderIds.Distinct().ToList();

        if (ids.Count == 0)
            return new Dictionary<long, OrderShipmentDto>();

        try
        {
            var shipments = await _context.OrderShipments
                .AsNoTracking()
                .Where(s => ids.Contains(s.OrderId) && s.Direction == OrderShipment.DirectionReverse)
                .ToListAsync(cancellationToken);

            return shipments.ToDictionary(s => s.OrderId, s => ForCustomer(ToDto(s)));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The page that asked is gone - not this service's answer to give (see PincodeService).
            throw;
        }
        catch (Exception exception)
        {
            // The same belt and braces as the forward read: the orders on the page are the screen, the parcels
            // are the extra (see GetForOrdersAsync).
            _logger.LogWarning(exception, "Reading the return parcels of {OrderCount} orders failed.", ids.Count);

            return new Dictionary<long, OrderShipmentDto>();
        }
    }

    /// <summary>
    /// Records the parcel the shop team booked in the provider's panel. Only this side's record is
    /// written: the consignment itself is created by hand in the provider's panel, exactly as before.
    ///
    /// A new AWB (or a new provider for the same AWB) clears the tracking state, because the old status
    /// belonged to another parcel - leaving it would show the customer the journey of a parcel that is
    /// not theirs until the next pull overwrote it.
    ///
    /// What the courier billed for the parcel is recorded in the same write, when the team has it: it is
    /// the shop's own figure for the books, so the customer-facing reads strip it (see ForCustomer), and
    /// an amount the request does not carry leaves whatever is already recorded alone - a form that does
    /// not ask for it can never wipe it.
    ///
    /// A parcel is recorded per leg (see <c>OrderShipment.DirectionForward/DirectionReverse</c>): the
    /// request's <c>Direction</c> picks which one, blank meaning the parcel that went out - so the Return
    /// card records a return's pickup with the very same call the Shipment card records a delivery with.
    /// </summary>
    public async Task<OrderShipmentDto> SaveAsync(
        long orderId,
        SaveOrderShipmentRequest request,
        long currentUserId,
        CancellationToken cancellationToken = default)
    {
        var awb = (request.AwbNumber ?? string.Empty).Trim();
        var courier = Trim(request.CourierName, CourierMaxLength);
        var trackingUrl = Trim(request.TrackingUrl, TrackingUrlMaxLength);

        // A parcel is either the one going out or the one coming back. Anything else is a typo, and
        // quietly writing it as 'Forward' would record a return's pickup as the delivery.
        if (!OrderShipment.IsValidDirection(request.Direction))
        {
            return Failed(orderId,
                $"'{request.Direction!.Trim()}' is not a parcel direction. Use " +
                $"'{OrderShipment.DirectionForward}' for the parcel going out or " +
                $"'{OrderShipment.DirectionReverse}' for the one coming back.");
        }

        // What the courier billed for the parcel is optional - the provider's panel shows it when the
        // parcel is billed, which can be after the team dispatched it - but a negative charge is always a
        // keystroke slip, so it is refused rather than written into the books.
        if (request.FreightCharge is < 0)
        {
            return Failed(orderId,
                "The freight charge cannot be negative - enter what the courier billed for this parcel, " +
                "or leave it blank.");
        }

        // A named provider has to be one this shop is actually set up with - otherwise the AWB would be
        // recorded against a provider that can never track it, and nobody would find out until a customer
        // asked. A blank one means the default, which is what a shop booking with a single aggregator
        // always sends.
        var provider = _providers.Find(request.Provider);
        if (provider == null && !string.IsNullOrWhiteSpace(request.Provider))
        {
            return Failed(orderId,
                $"'{request.Provider.Trim()}' is not a shipping provider this shop is set up with. Use one of: " +
                $"{string.Join(", ", _providers.All.Select(p => p.Name))}.");
        }

        provider ??= _providers.Default;

        if (provider == null)
        {
            return Failed(orderId,
                "No shipping provider is configured, so a parcel cannot be recorded. Set " +
                $"'{ShipmentProviderRegistry.DefaultProviderKey}' and the provider's own settings.");
        }

        var now = DateTime.UtcNow;

        // The provider is resolved first because it is what decides whether a blank AWB is a number that
        // has not been typed in yet or one this side mints itself: a provider with no panel behind it
        // (the shop's own delivery service) gives the parcel the shop's own reference, and everything
        // below - the customer's quote, the two screens, the tracking pull - reads it like any courier's
        // number (see IShipmentProvider.AwbGeneratedBySystem).
        if (awb.Length == 0 && provider.AwbGeneratedBySystem)
            awb = provider.CreateAwb(orderId, now).Trim();

        if (awb.Length == 0)
            return Failed(orderId, "Enter the AWB number the courier gave this parcel.");

        if (awb.Length > AwbMaxLength)
            return Failed(orderId, $"That AWB number is longer than {AwbMaxLength} characters - please check it.");

        var order = await _context.Orders
            .Include(o => o.OrderStatus)
            .FirstOrDefaultAsync(o => o.OrderId == orderId, cancellationToken);

        if (order == null)
            return Failed(orderId, "Order not found.");

        // Which leg this call is about - the parcel that went out unless the caller (the Return card)
        // asked for the one coming back.
        var direction = OrderShipment.NormaliseDirection(request.Direction);

        var shipment = await _context.OrderShipments
            .FirstOrDefaultAsync(s => s.OrderId == orderId && s.Direction == direction, cancellationToken);

        if (shipment == null)
        {
            shipment = new OrderShipment { OrderId = orderId, Direction = direction, CreatedOn = now };
            _context.OrderShipments.Add(shipment);
        }

        var isAnotherParcel =
            !string.Equals(shipment.AwbNumber ?? string.Empty, awb, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(shipment.Provider, provider.Name, StringComparison.OrdinalIgnoreCase);

        if (isAnotherParcel)
        {
            shipment.ProviderStatus = null;
            shipment.ProviderStatusCode = null;
            shipment.LastStatusText = null;
            shipment.DeliveredOn = null;
            shipment.LastCheckedOn = null;
        }

        shipment.Provider = Trim(provider.Name, ProviderMaxLength) ?? provider.Name;
        shipment.AwbNumber = awb;
        shipment.CourierName = courier;
        shipment.TrackingUrl = trackingUrl;
        shipment.ShiprocketShipmentId = request.ShiprocketShipmentId ?? shipment.ShiprocketShipmentId;

        // The freight is written only when the request carries one, so a form that does not ask for it
        // (the Shipped move) can never wipe a figure the Shipment card already recorded. Rounded to the
        // paisa, which is what the column holds.
        if (request.FreightCharge is { } freight)
        {
            shipment.FreightCharge = Math.Round(freight, 2, MidpointRounding.AwayFromZero);
        }

        shipment.UpdatedOn = now;

        // A parcel the shop carries itself takes its starting point from the order it belongs to, because the
        // order is the only thing that knows where such a parcel has got to (see
        // IShipmentProvider.ReportsTracking). Recording one against an order that has already been dispatched
        // - or has already arrived - therefore leaves the parcel saying where it is, rather than saying
        // nothing until the order happens to move again. No delivery date is offered with it: a parcel written
        // down after the fact must not claim it arrived today (see StampOwnDelivery). A courier-carried parcel
        // is not touched here: its own provider is asked, and tells the truth at the next pull.
        var ownDeliveryStage = provider.ReportsTracking
            ? (ShipmentStage?)null
            : StampOwnDelivery(shipment, order.OrderStatusId, now, deliveredOn: null);

        var loginId = await AdminLoginIdAsync(currentUserId, cancellationToken);
        var recorded = $"Parcel booked with {provider.Describe().DisplayName}: AWB {awb}" +
                       (courier.Length > 0 ? $" ({courier})" : string.Empty) + "." +
                       (ownDeliveryStage is { } ownDeliveryWhere
                           ? $" Recorded as '{ShipmentStatusFlow.Describe(ownDeliveryWhere)}', which is where " +
                             "the order already is."
                           : string.Empty);

        _context.OrderHistories.Add(new OrderHistory
        {
            OrderId = order.OrderId,
            HistoryDate = now,
            OrderStatusId = order.OrderStatusId,
            Comments = loginId == null ? recorded : $"{recorded} (by {loginId})"
        });

        await _context.SaveChangesAsync(cancellationToken);

        return ToDto(shipment, order.OrderStatusId, order.OrderStatus?.Status, result: 1, new[] { recorded });
    }

    /// <summary>
    /// True when this request really records a parcel. It is the provider's own answer, not a rule the
    /// caller can apply: a consignment number the team typed is a parcel whoever carries it, and a blank
    /// one is a parcel only against a provider that mints the reference itself (the shop's own service).
    /// The admin's Shipped move asks before it writes anything, so a dispatch with no parcel at all never
    /// records one - and never keeps a freight charge that would have nowhere to live.
    /// </summary>
    public bool RecordsParcel(SaveOrderShipmentRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.AwbNumber))
            return true;

        // Resolved exactly as SaveAsync resolves it: the named provider, or the shop's default when none
        // is named - and a named one that is not registered is not a parcel this side could write.
        var provider = _providers.Find(request.Provider);

        if (provider == null && !string.IsNullOrWhiteSpace(request.Provider))
            return false;

        return (provider ?? _providers.Default)?.AwbGeneratedBySystem ?? false;
    }

    /// <summary>
    /// Brings a parcel the shop carries itself level with the order that has just been moved - the shop
    /// team's own status move (see AdminDashboardService.UpdateOrderStatusAsync). Such a parcel has no
    /// courier to ask about it, ever: the move the team just made IS its movement, so this writes that move
    /// on the parcel - 'on the way' when the order was dispatched, 'Delivered' and the day it arrived when it
    /// was delivered - and says so in the answer, because the team should read on the order screen what the
    /// customer reads on theirs.
    ///
    /// A courier-carried parcel is left exactly as it is: it is asked, never assumed (see
    /// <see cref="RefreshAsync"/>), and the last thing a real lookup wrote is not this method's to overwrite.
    /// An order with no parcel recorded, and a move that says nothing about a parcel (Confirmed, Cancelled),
    /// answer with "" and save nothing.
    /// </summary>
    public async Task<string> MirrorOrderStatusAsync(
        long orderId, short orderStatusId, DateTime now, CancellationToken cancellationToken = default)
    {
        // Only the parcel that went out: the order follows its own delivery, so it is that leg which is
        // mirrored - a return's own leg is reported on by the courier carrying it (or by the shop team
        // recording the pickup), never by the order's status.
        var shipment = await _context.OrderShipments
            .FirstOrDefaultAsync(s => s.OrderId == orderId && s.Direction == OrderShipment.DirectionForward, cancellationToken);

        // Nothing was ever recorded for this order, or a courier carries it and reports on it itself: either
        // way there is nothing here to bring level.
        if (shipment == null || ReportsTrackingFor(shipment.Provider))
            return string.Empty;

        if (StampOwnDelivery(shipment, orderStatusId, now, deliveredOn: now) is not { } stage)
            return string.Empty;

        await _context.SaveChangesAsync(cancellationToken);

        return $"The parcel {shipment.AwbNumber} is recorded as '{ShipmentStatusFlow.Describe(stage)}' with it.";
    }

    /// <summary>
    /// Asks the courier about this order's parcel right now - the 'Track now' button on the admin order
    /// screen. The throttle that protects the customer-facing pull is deliberately not applied here.
    /// </summary>
    public async Task<OrderShipmentDto> RefreshAsync(
        long orderId, long currentUserId, CancellationToken cancellationToken = default)
    {
        var order = await _context.Orders
            .Include(o => o.OrderStatus)
            .Include(o => o.OrderItems)
            .Include(o => o.OrderShipments)
            .FirstOrDefaultAsync(o => o.OrderId == orderId, cancellationToken);

        if (order == null)
            return Failed(orderId, "Order not found.");

        var shipment = ForwardLeg(order.OrderShipments);

        if (shipment == null || string.IsNullOrWhiteSpace(shipment.AwbNumber))
        {
            return Failed(orderId,
                "No AWB is recorded for this order yet, so there is nothing to track. Record the parcel first.");
        }

        return await RefreshOneAsync(order, shipment, DateTime.UtcNow, currentUserId, cancellationToken);
    }

    /// <summary>
    /// The pull 'My Orders' makes on open: every live parcel of this customer that is due a lookup is
    /// asked about once, and the answer is handed back so the page can show it without another round trip.
    ///
    /// It follows both of an order's legs. The parcel that went out is looked at while the order can still
    /// follow it (Confirmed and Shipped - see <see cref="ShipmentStatusFlow.CanOrderFollow"/>), and the parcel
    /// coming back is looked at once a return has been approved, so a customer who sent something back can see
    /// where it is. Each parcel is only asked about when its own throttle has run out and the courier has not
    /// finished with it; a parcel that fails is answered with the last thing that was known about it plus why it
    /// could not be refreshed, so the page never loses a status it already had. Nothing here throws: a courier
    /// that is down must not stop 'My Orders' rendering.
    /// </summary>
    public async Task<RefreshOrderShipmentsDto> RefreshForCustomerAsync(
        long customerId, CancellationToken cancellationToken = default)
    {
        // Provider-neutral 'still moving' statuses, written out because EF translates members, not method
        // calls: Confirmed (the parcel may have been collected), Shipped (it may have arrived) and Delivered -
        // which is where a return starts, so the parcel coming back has an order to be followed from. Delivered
        // orders only ever contribute that second leg: nothing below lets an order that arrived move again.
        var orders = await _context.Orders
            .Include(o => o.OrderStatus)
            .Include(o => o.OrderItems)
            .Include(o => o.OrderShipments)
            .Include(o => o.OrderReturns)
            .Where(o => o.CustomerId == customerId &&
                        (o.OrderStatusId == OrderStatusFlow.Confirmed ||
                         o.OrderStatusId == OrderStatusFlow.Shipped ||
                         o.OrderStatusId == OrderStatusFlow.Delivered))
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var result = new RefreshOrderShipmentsDto { Result = 1 };

        // One parcel, looked up and handed on as the customer may read it (the shop's own freight figure is
        // stripped - see ForCustomer). A failure costs the page its parcel and never the page itself.
        async Task PullAsync(Order order, OrderShipment parcel)
        {
            OrderShipmentDto answer;

            try
            {
                answer = await RefreshOneAsync(order, parcel, now, currentUserId: 0, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // The page that asked is gone - not this service's answer to give (see PincodeService).
                throw;
            }
            catch (Exception exception)
            {
                // Belt and braces: the provider call never throws, but writing down what it said can.
                // One parcel must never cost the customer the rest of the page.
                _logger.LogWarning(exception,
                    "Refreshing the parcel of order {OrderId} failed while it was being written down.",
                    order.OrderId);

                return;
            }

            result.Shipments.Add(ForCustomer(answer));

            if (answer.Result == 0)
            {
                result.Messages = result.Messages
                    .Append(answer.Messages.FirstOrDefault() ??
                            "This parcel could not be checked with the courier just now.")
                    .ToArray();
            }
        }

        foreach (var order in orders)
        {
            // The parcel the customer is waiting for, if the order can still follow it. The query above is only a
            // prefilter written out for EF (it translates members, not method calls); the lifecycle still decides,
            // so an order that has already arrived is not moved again by a courier scan.
            var forward = ForwardLeg(order.OrderShipments);

            if (forward != null && ShipmentStatusFlow.CanOrderFollow(order.OrderStatusId) &&
                IsDue(forward, now) && ReportsTrackingFor(forward.Provider))
            {
                await PullAsync(order, forward);
            }

            // The parcel coming back, once a return has been approved: the pickup the shop team booked (or the
            // courier's own return-to-origin) is followed too, so 'My Orders' says where what the customer sent
            // back has got to. Only an approved return has a parcel to follow - an ask still waiting for the shop
            // team's answer has none - and a parcel with no courier behind it is never looked up at all (see
            // IShipmentProvider.ReportsTracking).
            var openReturn = order.OrderReturns
                .OrderByDescending(r => r.ReturnId)
                .FirstOrDefault();

            var reverse = ReverseLeg(order.OrderShipments);

            if (openReturn != null && OrderReturnStatus.WasApproved(openReturn.Status) &&
                reverse != null && IsDue(reverse, now) && ReportsTrackingFor(reverse.Provider))
            {
                await PullAsync(order, reverse);
            }
        }

        return result;
    }

    /// <summary>
    /// The pull itself, for one parcel: ask the provider, write down what it said, and let the order and
    /// its units follow it when the courier's status allows a move. Both the customer-facing pull and the
    /// shop team's 'Track now' come through here, so there is one definition of what a courier scan does
    /// to an order - exactly as <see cref="SkuAvailability"/> is the one definition for the stock.
    /// </summary>
    private async Task<OrderShipmentDto> RefreshOneAsync(
        Order order,
        OrderShipment shipment,
        DateTime now,
        long currentUserId,
        CancellationToken cancellationToken)
    {
        var awb = (shipment.AwbNumber ?? string.Empty).Trim();
        var statusName = await StatusNameAsync(order.OrderStatusId, cancellationToken);

        // The parcel's own provider, or the default when the one it was booked with is no longer
        // registered - taking an adapter out of configuration must never leave an existing parcel
        // untrackable (see IShipmentProviderRegistry).
        var provider = _providers.Find(shipment.Provider) ?? _providers.Default;

        if (provider == null)
        {
            return ToDto(shipment, order.OrderStatusId, statusName, result: 0, new[]
            {
                "This shop has no shipping provider set up, so the parcel cannot be tracked."
            });
        }

        // A provider with no courier behind it is never called: there is nothing at the other end, so a
        // lookup would only write a failure down for the person waiting (see
        // IShipmentProvider.ReportsTracking). LastCheckedOn is deliberately left alone - no check was made,
        // so the parcel is not throttled out of a real one later.
        if (!provider.ReportsTracking)
        {
            return ToDto(shipment, order.OrderStatusId, statusName, result: 0, new[]
            {
                $"{provider.Describe().DisplayName} carries this parcel itself, so there is no courier to " +
                $"ask about {awb}. Move the order along from the status list when it moves."
            });
        }

        if (!provider.IsConfigured)
        {
            return ToDto(shipment, order.OrderStatusId, statusName, result: 0, new[]
            {
                $"{provider.Describe().DisplayName} tracking is not set up on this server, so AWB {awb} " +
                "cannot be checked right now - the last status we have is shown instead."
            });
        }

        var snapshot = await provider.TrackByAwbAsync(awb, cancellationToken);

        // The attempt counts as a check whatever it answered: a provider that is down must not be asked
        // again by every page open (the throttle reads this column). What it did not answer is left
        // untouched, so a failed lookup can never wipe a status the customer has already been shown.
        shipment.LastCheckedOn = now;

        if (!snapshot.Succeeded)
        {
            _logger.LogWarning("Tracking AWB '{Awb}' with the '{Provider}' provider failed: {Message}",
                awb, provider.Name, snapshot.Message);

            await _context.SaveChangesAsync(cancellationToken);

            return ToDto(shipment, order.OrderStatusId, statusName, result: 0, new[] { snapshot.Message });
        }

        shipment.UpdatedOn = now;

        // The courier's own wording is kept verbatim: everything decided here is read back out of this
        // text (see ShipmentStatusFlow), never translated on the way in, so a renamed provider status
        // can only ever leave the order alone.
        shipment.ProviderStatus = Trim(snapshot.StatusText, ProviderStatusMaxLength);
        shipment.ProviderStatusCode = snapshot.StatusCode;
        shipment.LastStatusText = Trim(snapshot.StatusText, LastStatusTextMaxLength);

        // The courier and the tracking link are written only when the provider reports them, so a
        // provider that stays quiet cannot erase what the shop team typed when they booked the parcel.
        if (!string.IsNullOrWhiteSpace(snapshot.CourierName))
            shipment.CourierName = Trim(snapshot.CourierName, CourierMaxLength);

        if (!string.IsNullOrWhiteSpace(snapshot.TrackingUrl))
            shipment.TrackingUrl = Trim(snapshot.TrackingUrl, TrackingUrlMaxLength);

        var stage = ShipmentStatusFlow.FromProviderText(shipment.ProviderStatus);
        shipment.DeliveredOn = ShipmentStatusFlow.DeliveredOn(stage, snapshot.DeliveredOn, now);

        var message = PullMessage(awb, stage, shipment.ProviderStatus);

        // Only the parcel that went out can move the order. A reverse leg's own 'Delivered' means the customer's
        // parcel reached the SHOP, not that the order was delivered - reading it as the order arriving would undo
        // the very return it is carrying - so nothing is proposed for a leg coming back.
        var proposal = shipment.Direction == OrderShipment.DirectionForward
            ? ShipmentStatusFlow.OrderStatusFor(stage)
            : null;

        if (proposal is { } target)
        {
            if (OrderStatusFlow.CanMove(order.OrderStatusId, target))
            {
                var followed = await FollowTheCourierAsync(order, target, shipment.ProviderStatus,
                    now, currentUserId, cancellationToken);

                message += " " + followed;
            }
            else
            {
                // The courier asked for a move the order's lifecycle does not allow - an unpaid order a
                // scan tried to ship, or a parcel delivered on an order the team already wrote off. The
                // parcel status above is kept and the order is left alone, but the shop team is told:
                // silence here is how a courier scan and a screen end up disagreeing.
                var proposedName = await StatusNameAsync(target, cancellationToken);

                message += $" The courier's status would move the order to {proposedName}, but this order " +
                           $"is {statusName} - it has been left as it is.";
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        // A refusal or a return-to-origin is the courier saying the parcel is coming back, and that is written down
        // as a return waiting for the shop team's answer - never as a decision (see RaiseReturnAskAsync).
        var asked = await RaiseReturnAskAsync(
            order, shipment, stage, shipment.ProviderStatus, now, cancellationToken);

        if (asked.Length > 0)
            message += " " + asked;

        return ToDto(shipment, order.OrderStatusId,
            await StatusNameAsync(order.OrderStatusId, cancellationToken), result: 1, new[] { message });
    }

    /// <summary>
    /// Writes down the return a courier's own status asks for, when it asks for one: a parcel refused at the
    /// doorstep, or one coming back undelivered (see <see cref="ShipmentStatusFlow.ReturnReasonFor"/> and
    /// <see cref="OrderReturnReason.IsCourierReason"/>). The ask is the courier's own (Origin 'Courier'), it only
    /// ever ASKS - the shop team decides it - and it goes where a customer's own ask goes, so both are answered
    /// from the same Return card.
    ///
    /// Three things are deliberately left out. An order that has already given its units back (cancelled, or a
    /// previous return) raises nothing: the parcel coming back is not a new event for it, and its money is
    /// already settled. The parcel coming back raises nothing either - a reverse leg IS that return's parcel, not
    /// a second ask about it. And a refusal or RTO the courier keeps reporting raises nothing after the first
    /// time: the order already has an open return, and the shop team is reminded rather than told about a second
    /// one (only one can exist - see OrderReturnFlow.FindOpenAsync).
    ///
    /// Returns the sentence for the person who asked (the customer's page, or the shop team's 'Track now'), or ""
    /// when no ask was raised.
    /// </summary>
    private async Task<string> RaiseReturnAskAsync(
        Order order,
        OrderShipment shipment,
        ShipmentStage stage,
        string providerStatus,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (shipment.Direction != OrderShipment.DirectionForward ||
            ShipmentStatusFlow.ReturnReasonFor(stage) is not { } reasonCode ||
            SkuAvailability.ReleasesUnits(order.OrderStatusId))
        {
            return string.Empty;
        }

        var alreadyOpen = await OrderReturnFlow.FindOpenAsync(_context, order.OrderId, cancellationToken);

        if (alreadyOpen != null)
        {
            return $"A return of this order is already waiting to be answered " +
                   $"({OrderReturnReason.Label(alreadyOpen.ReasonCode)}).";
        }

        var asked = await OrderReturnFlow.RequestAsync(
            _context,
            order,
            OrderReturnOrigin.Courier,
            reasonCode,
            providerStatus.Length > 0 ? providerStatus : OrderReturnReason.Label(reasonCode),
            requestedBy: null,
            now,
            cancellationToken);

        _logger.LogInformation(
            "The courier's own status of order {OrderId} raised return {ReturnId} for the shop team ({ReasonCode}).",
            order.OrderId, asked.ReturnId, reasonCode);

        return $"A return is now waiting to be answered ({OrderReturnReason.Label(reasonCode)}): the parcel is " +
               "coming back to us.";
    }

    /// <summary>
    /// Moves the order onto the courier's status, with its units, and writes the one history row the
    /// customer reads. It is the shop team's own move (see AdminDashboardService.UpdateOrderStatusAsync),
    /// in the same order - stock first, then the status, then the note - so the two can never leave the
    /// order and the stock counters describing different things. Returns the sentence for the answer.
    /// </summary>
    private async Task<string> FollowTheCourierAsync(
        Order order,
        short targetStatusId,
        string providerStatus,
        DateTime now,
        long currentUserId,
        CancellationToken cancellationToken)
    {
        var movedUnits = await SkuAvailability.MoveUnitsForOrderStatusAsync(
            _context, order, targetStatusId, now, cancellationToken);

        order.OrderStatusId = targetStatusId;

        var statusName = await StatusNameAsync(targetStatusId, cancellationToken);
        var loginId = await AdminLoginIdAsync(currentUserId, cancellationToken);

        _context.OrderHistories.Add(new OrderHistory
        {
            OrderId = order.OrderId,
            HistoryDate = now,
            OrderStatusId = targetStatusId,
            Comments = FollowedComment(targetStatusId, statusName, providerStatus, loginId)
        });

        var units = movedUnits switch
        {
            0 => string.Empty,
            1 => " 1 unit updated.",
            _ => $" {movedUnits} units updated."
        };

        return $"The order is now {statusName}.{units}";
    }

    /// <summary>
    /// The sentence a pull answers with: the shop's own wording when it has one for what the courier
    /// said, and the courier's own words when it does not (an unknown wording is never guessed at).
    /// </summary>
    private static string PullMessage(string awb, ShipmentStage stage, string providerStatus)
    {
        var words = ShipmentStatusFlow.Describe(stage);

        if (words.Length > 0)
            return $"AWB {awb}: {words}.";

        return providerStatus.Length > 0
            ? $"AWB {awb}: the courier reports \"{providerStatus}\"."
            : $"AWB {awb}: the courier has not reported a status yet.";
    }

    /// <summary>
    /// The order history's version of a tracking move, in the customer's voice - it is read in 'My
    /// Orders' next to the shop team's own notes, and it says where the wording came from, because an
    /// order that moved by itself should not read like a person moved it.
    /// </summary>
    private static string FollowedComment(
        short newStatusId, string statusName, string providerStatus, string? loginId)
    {
        var what = newStatusId switch
        {
            OrderStatusFlow.Shipped => "Order dispatched",
            OrderStatusFlow.Delivered => "Order delivered",
            _ => $"Order moved to {statusName}"
        };

        var reported = providerStatus.Length > 0
            ? $" - the courier reports \"{providerStatus}\""
            : string.Empty;

        var by = loginId == null
            ? " (from the courier's tracking)"
            : $" (from the courier's tracking, by {loginId})";

        return $"{what}{reported}{by}";
    }

    /// <summary>
    /// True when a parcel is due a lookup: one has been recorded, the courier is not done with it, and it
    /// was last looked at longer ago than the throttle allows (a parcel never looked at is always due). A
    /// 'LastCheckedOn' in the future - a clock that moved - counts as not due, so a parcel can never be
    /// asked about in a loop.
    /// </summary>
    private bool IsDue(OrderShipment shipment, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(shipment.AwbNumber))
            return false;

        if (ShipmentStatusFlow.IsClosed(ShipmentStatusFlow.FromProviderText(shipment.ProviderStatus)))
            return false;

        return shipment.LastCheckedOn == null || now - shipment.LastCheckedOn.Value >= _syncThrottle;
    }

    /// <summary>
    /// True when the provider a parcel was booked with can be asked where it is: the adapter it names, or
    /// the default when that one is no longer registered - resolved exactly as a lookup resolves it, so the
    /// answer the screens read and the lookup the pull makes can never disagree. False for a parcel carried
    /// by a service the shop arranges itself (see IShipmentProvider.ReportsTracking).
    /// </summary>
    private bool ReportsTrackingFor(string? providerName) =>
        (_providers.Find(providerName) ?? _providers.Default)?.ReportsTracking ?? false;

    /// <summary>
    /// Writes down where a parcel the shop carries itself has got to, from the order status that has just
    /// been set - and answers the stage it wrote, or null when there was nothing to write.
    ///
    /// It is the one place a shop-side move becomes a parcel status (see
    /// ShipmentStatusFlow.OwnDeliveryStageFor), so the order screen, 'My Orders' and the team's own answer
    /// can never tell three different stories about one parcel.
    ///
    /// <paramref name="deliveredOn"/> is the day the parcel reached the customer, where that is known: the
    /// move that carried it out passes its own moment (see MirrorOrderStatusAsync), while a parcel written
    /// down after the fact passes nothing (see SaveAsync) and is then recorded as delivered without a date,
    /// rather than dated with whenever somebody happened to fill the form in - the one date 'My Orders' shows
    /// must never claim a parcel arrived when it did not (see OrderShipment.DeliveredOn).
    ///
    /// Three other details are deliberate: the courier's own columns are cleared and no last-checked time is
    /// set, because there was no check and nobody to make one (any time left over from another provider was
    /// already cleared when the parcel was written under this one - see SaveAsync); a parcel that already says
    /// this, on the same date, is left exactly as it is; and a move that says nothing about a parcel writes
    /// nothing at all. Those last two are what make the Shipped move safe, where the parcel is recorded and
    /// the move mirrored in the same breath.
    /// </summary>
    private static ShipmentStage? StampOwnDelivery(
        OrderShipment shipment, short orderStatusId, DateTime now, DateTime? deliveredOn)
    {
        if (ShipmentStatusFlow.OwnDeliveryStageFor(orderStatusId) is not { } stage)
            return null;

        var text = ShipmentStatusFlow.OwnDeliveryText(stage);

        // The first party to know a delivery date wins, and none of the others can move it or wipe it by
        // having nothing to offer: whichever move put the parcel in the customer's hands is the one that dates
        // it. Every other stage carries no date at all - this column must never claim an arrival.
        var arrivesOn = stage == ShipmentStage.Delivered
            ? shipment.DeliveredOn ?? deliveredOn
            : null;

        if (string.Equals(shipment.ProviderStatus, text, StringComparison.Ordinal) &&
            shipment.DeliveredOn == arrivesOn)
        {
            return null;
        }

        shipment.ProviderStatus = text;
        shipment.ProviderStatusCode = null;
        shipment.LastStatusText = null;
        shipment.DeliveredOn = arrivesOn;
        shipment.UpdatedOn = now;

        return stage;
    }

    /// <summary>
    /// A shipment row as both screens read it. <c>Status</c> is the shop's own wording for where the
    /// parcel is, and is deliberately empty when the shop has none for what the courier said - the screen
    /// falls back to <c>ProviderStatus</c>, which is the courier's sentence verbatim.
    /// </summary>
    private OrderShipmentDto ToDto(
        OrderShipment shipment,
        short? orderStatusId = null,
        string? orderStatus = null,
        int result = 1,
        string[]? messages = null)
    {
        var stage = ShipmentStatusFlow.FromProviderText(shipment.ProviderStatus);

        return new OrderShipmentDto
        {
            Result = result,
            Messages = messages ?? Array.Empty<string>(),
            OrderId = shipment.OrderId,
            Direction = shipment.Direction,
            IsReverse = shipment.Direction == OrderShipment.DirectionReverse,
            HasShipment = !string.IsNullOrWhiteSpace(shipment.AwbNumber),
            Provider = shipment.Provider ?? string.Empty,
            ReportsTracking = ReportsTrackingFor(shipment.Provider),
            CourierName = shipment.CourierName ?? string.Empty,
            AwbNumber = shipment.AwbNumber ?? string.Empty,
            TrackingUrl = shipment.TrackingUrl ?? string.Empty,
            FreightCharge = shipment.FreightCharge,
            ProviderStatus = shipment.ProviderStatus ?? string.Empty,
            ProviderStatusCode = shipment.ProviderStatusCode,
            Status = ShipmentStatusFlow.Describe(stage),
            Stage = stage.ToString(),
            Delivered = stage == ShipmentStage.Delivered,
            Closed = ShipmentStatusFlow.IsClosed(stage),
            DeliveredOn = shipment.DeliveredOn,
            LastStatusText = shipment.LastStatusText ?? string.Empty,
            LastCheckedOn = shipment.LastCheckedOn,
            OrderStatusId = orderStatusId,
            OrderStatus = orderStatus
        };
    }

    /// <summary>
    /// A parcel as the customer may read it: what the courier billed the shop for it is removed here,
    /// because that figure is the shop's own business and not the customer's (see
    /// OrderShipmentDto.FreightCharge). Both customer-facing reads go through this on the way out - the
    /// list 'My Orders' reads and the pull it makes when it opens - so no later change to a mapping can
    /// leak it, and the admin's own reads are left with it.
    /// </summary>
    private static OrderShipmentDto ForCustomer(OrderShipmentDto parcel)
    {
        parcel.FreightCharge = null;

        return parcel;
    }

    /// <summary>
    /// The parcel that went out, out of the legs one order may carry (see
    /// <c>OrderShipment.DirectionForward/DirectionReverse</c>): until a return is arranged an order has
    /// only this one, and afterwards 'the parcel' still means this one unless a caller asks for the other
    /// by name.
    /// </summary>
    private static OrderShipment? ForwardLeg(IEnumerable<OrderShipment> legs) =>
        legs.FirstOrDefault(s => s.Direction == OrderShipment.DirectionForward);

    /// <summary>
    /// The parcel coming back, out of the legs one order may carry: a return's own leg. Null for every order that
    /// has never had a return arranged - which is most of them - so callers can simply skip it.
    /// </summary>
    private static OrderShipment? ReverseLeg(IEnumerable<OrderShipment> legs) =>
        legs.FirstOrDefault(s => s.Direction == OrderShipment.DirectionReverse);

    /// <summary>Nothing could be done, and here is why - in words for the person who asked.</summary>
    private static OrderShipmentDto Failed(long orderId, string message) => new()
    {
        Result = 0,
        OrderId = orderId,
        Messages = new[] { message }
    };

    /// <summary>Trimmed to fit the column it is written into, never longer (see the lengths above).</summary>
    private static string Trim(string? value, int maxLength)
    {
        var text = (value ?? string.Empty).Trim();

        return text.Length <= maxLength ? text : text[..maxLength];
    }

    /// <summary>The lifecycle step's name, in the lookup table's own words ("" when the id is unknown).</summary>
    private async Task<string> StatusNameAsync(short statusId, CancellationToken cancellationToken) =>
        await _context.OrderStatuses
            .Where(s => s.OrderStatusId == statusId)
            .Select(s => s.Status)
            .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

    /// <summary>The login id of the person behind a change, so the order history says who did it.</summary>
    private async Task<string?> AdminLoginIdAsync(long userId, CancellationToken cancellationToken)
    {
        if (userId <= 0)
            return null;

        return await _context.Users
            .Where(u => u.UserId == userId)
            .Select(u => u.LoginId)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
