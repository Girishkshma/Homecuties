// ============================================================
// OrderService.Orders.cs
// Partial class: OrderService - Orders operations
// ============================================================

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
    /// <summary>
    /// Places an order. Everything - locking the required SKUs, creating the order and its items,
    /// moving the SKUs to "Ordered" (with SKU history) and updating the cart - runs inside ONE
    /// database transaction. If any stage fails the whole thing is rolled back, so no stock is
    /// consumed and no half-built order is left behind.
    /// </summary>
    public async Task<CreateOrderResponse> CreateOrderAsync(CreateOrderRequest request)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var response = await CreateOrderInternalAsync(request);

            if (response.Result == 1)
            {
                await transaction.CommitAsync();
            }
            else
            {
                await transaction.RollbackAsync();
                _context.ChangeTracker.Clear();
            }

            return response;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _context.ChangeTracker.Clear();

            return new CreateOrderResponse
            {
                Result = 0,
                Messages = new[]
                {
                    "We could not place your order. Nothing was changed - your cart and the stock are exactly as they were. Please try again.",
                    ex.GetBaseException().Message
                }
            };
        }
    }

    private async Task<CreateOrderResponse> CreateOrderInternalAsync(CreateOrderRequest request)
    {
        // Refuse an online-payment order before anything is reserved or written when the gateway is
        // not configured. Otherwise the customer is handed a payment order that Razorpay rejects
        // (its API answers 401 for unknown keys) - and nothing could ever be paid for it.
        if (string.Equals(request.PaymentMethod, "razorpay", StringComparison.OrdinalIgnoreCase) &&
            !IsRazorpayConfigured)
        {
            _logger.LogError(
                "Order rejected: 'Razorpay:KeyId'/'Razorpay:KeySecret' are not configured, so the " +
                "payment gateway cannot be called.");

            return new CreateOrderResponse
            {
                Result = 0,
                Messages = new[] { "Online payment is not available right now. Please contact support." }
            };
        }

        // The length limits of the CustomerAddresses columns. An over-long value would only fail at
        // the INSERT - after the stock has been reserved - and the customer would be left with an
        // unexplained error, so it is refused here while nothing has changed yet.
        var addressProblem = ValidateTypedAddresses(request);
        if (addressProblem != null)
        {
            return new CreateOrderResponse { Result = 0, Messages = new[] { addressProblem } };
        }

        // Get the cart items
        CartResponseDto cartResponse;
        if (request.IsGuest)
        {
            var guestCart = await _context.Set<GuestCart>()
                .Include(gc => gc.GuestCartItems)
                    .ThenInclude(ci => ci.Product)
                        .ThenInclude(p => p.ProductImages)
                .FirstOrDefaultAsync(gc => gc.CustomerId == request.CustomerID);

            if (guestCart == null || !guestCart.GuestCartItems.Any())
                return new CreateOrderResponse { Result = 0, Messages = new[] { "Cart is empty" } };

            cartResponse = MapGuestCartToResponse(guestCart, await SkuAvailability.CountSellableByProductAsync(
                _context, guestCart.GuestCartItems.Select(ci => ci.ProductId)));
        }
        else
        {
            var cart = await _context.Carts
                .Include(c => c.CartItems)
                    .ThenInclude(ci => ci.Product)
                        .ThenInclude(p => p.ProductImages)
                .FirstOrDefaultAsync(c => c.CustomerId == request.CustomerID);

            if (cart == null || !cart.CartItems.Any())
                return new CreateOrderResponse { Result = 0, Messages = new[] { "Cart is empty" } };

            cartResponse = MapCartToResponse(cart, await SkuAvailability.CountSellableByProductAsync(
                _context, cart.CartItems.Select(ci => ci.ProductId)));
        }

        // Filter out out-of-stock items and adjust quantities to available stock
        var removedItems = new List<string>();
        var adjustedItems = new List<string>();
        var validItems = new List<CartItemDto>();

        foreach (var item in cartResponse.Items)
        {
            if (!item.IsInStock)
            {
                removedItems.Add(item.ProductTitle);
            }
            else if (item.Quantity > item.AvailableQty)
            {
                adjustedItems.Add($"{item.ProductTitle} (requested {item.Quantity}, available {item.AvailableQty})");
                item.Quantity = item.AvailableQty;
                validItems.Add(item);
            }
            else
            {
                validItems.Add(item);
            }
        }

        if (!validItems.Any())
        {
            return new CreateOrderResponse
            {
                Result = 0,
                Messages = new[] { "All items in your cart are currently out of stock. Please remove them and try again." }
            };
        }

        // Build messages about removed/adjusted items
        var messages = new List<string>();
        if (removedItems.Any())
        {
            messages.Add($"The following items are out of stock and have been removed: {string.Join(", ", removedItems)}");
        }
        if (adjustedItems.Any())
        {
            messages.Add($"Quantities adjusted for: {string.Join(", ", adjustedItems)}");
        }

        // Remove out-of-stock items from the cart and adjust quantities
        if (request.IsGuest)
        {
            var guestCart = await _context.Set<GuestCart>()
                .Include(gc => gc.GuestCartItems)
                .FirstOrDefaultAsync(gc => gc.CustomerId == request.CustomerID);

            if (guestCart != null)
            {
                var itemsToRemove = guestCart.GuestCartItems
                    .Where(ci => !validItems.Any(vi => vi.ProductID == ci.ProductId))
                    .ToList();
                foreach (var itemToRemove in itemsToRemove)
                {
                    guestCart.GuestCartItems.Remove(itemToRemove);
                }

                // Adjust quantities for items that had quantity reduced
                foreach (var adjusted in validItems.Where(v => v.Quantity < cartResponse.Items.First(i => i.ProductID == v.ProductID).Quantity))
                {
                    var cartItem = guestCart.GuestCartItems.FirstOrDefault(ci => ci.ProductId == adjusted.ProductID);
                    if (cartItem != null)
                    {
                        cartItem.Quantity = (short)adjusted.Quantity;
                    }
                }
            }
        }
        else
        {
            var cart = await _context.Carts
                .Include(c => c.CartItems)
                .FirstOrDefaultAsync(c => c.CustomerId == request.CustomerID);

            if (cart != null)
            {
                var itemsToRemove = cart.CartItems
                    .Where(ci => !validItems.Any(vi => vi.ProductID == ci.ProductId))
                    .ToList();
                foreach (var itemToRemove in itemsToRemove)
                {
                    cart.CartItems.Remove(itemToRemove);
                }

                // Adjust quantities for items that had quantity reduced
                foreach (var adjusted in validItems.Where(v => v.Quantity < cartResponse.Items.First(i => i.ProductID == v.ProductID).Quantity))
                {
                    var cartItem = cart.CartItems.FirstOrDefault(ci => ci.ProductId == adjusted.ProductID);
                    if (cartItem != null)
                    {
                        cartItem.Quantity = (short)adjusted.Quantity;
                    }
                }
            }
        }

        await _context.SaveChangesAsync();

        // Recalculate with valid items only
        cartResponse.Items = validItems;
        cartResponse.Calculation = RecalculateCart(validItems);

        // Guests only exist in GuestCustomers, while Orders and CustomerAddresses have a foreign
        // key to Customers. Resolve (or create) the matching customer record so a guest order
        // can actually be persisted.
        var orderCustomerId = request.IsGuest
            ? await ResolveGuestCustomerIdAsync(request)
            : request.CustomerID;

        // Where the order is delivered and what it is invoiced to. A saved address picked in checkout
        // is used as it stands; anything typed is stored in the customer's address book, so it can be
        // picked again for the next order instead of being typed from scratch.
        var shippingAddress = await ResolveShippingAddressAsync(orderCustomerId, request);
        if (shippingAddress == null)
        {
            return new CreateOrderResponse
            {
                Result = 0,
                Messages = new[]
                {
                    "Please give the address this order should be delivered to (address, city, state and PIN code)."
                }
            };
        }

        var billingAddress = await ResolveBillingAddressAsync(orderCustomerId, request, shippingAddress);
        if (billingAddress == null)
        {
            return new CreateOrderResponse
            {
                Result = 0,
                Messages = new[] { "Please check the billing address you entered (address, city, state and PIN code)." }
            };
        }

        // Create the order
        var order = new Order
        {
            CustomerId = orderCustomerId,
            SellerId = 1, // Default seller
            OrderDate = DateTime.UtcNow,
            BillingAddressId = billingAddress.AddressId,
            ShippingAddressId = shippingAddress.AddressId,
            OrderStatusId = 1 // Pending
        };
        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        // Create order items and reserve SKUs.
        // The required SKU rows are locked first, with (UPDLOCK, ROWLOCK, HOLDLOCK), and the locks
        // are held until the surrounding transaction commits or rolls back. This is what stops two
        // simultaneous checkouts from selling the same physical unit.
        // Products are locked in a deterministic (ProductID) order to avoid deadlocks.
        var reservedItems = new List<CartItemDto>();
        var stockChangedItems = new List<string>();
        var skuHistoryDate = DateTime.UtcNow;

        foreach (var item in validItems.OrderBy(i => i.ProductID))
        {
            var product = await _context.Products
                .Include(p => p.ProductCategories)
                .FirstOrDefaultAsync(p => p.ProductId == item.ProductID);

            if (product == null)
            {
                continue;
            }

            var reservedSkus = await LockAvailableSkusAsync(item.ProductID, item.Quantity);

            if (reservedSkus.Count == 0)
            {
                // Stock disappeared while the customer was checking out.
                stockChangedItems.Add($"{item.ProductTitle} (out of stock)");
                continue;
            }

            if (reservedSkus.Count < item.Quantity)
            {
                // Only charge for the units we actually locked.
                stockChangedItems.Add($"{item.ProductTitle} (requested {item.Quantity}, reserved {reservedSkus.Count})");
                item.Quantity = reservedSkus.Count;
            }

            foreach (var sku in reservedSkus)
            {
                // Associate the physical unit with this order.
                _context.OrderItems.Add(new OrderItem
                {
                    OrderId = order.OrderId,
                    Sku = sku.Sku1,
                    ProductName = product.ProductName,
                    ProductTitle = product.ProductTitle,
                    ProductDescription = product.ProductDescription,
                    ProductCategoryId = product.ProductCategories.FirstOrDefault()?.CategoryId ?? 1,
                    UnitPrice = product.UnitPrice,
                    Hsncode = product.Hsncode,
                    PackagingCharge = product.PackagingCharge,
                    StorageCharge = product.StorageCharge,
                    DiscountPercent = product.DiscountPercent,
                    AdditionalDiscountPercent = product.AdditionalDiscountPercent,
                    DeliveryCharge = product.DeliveryCharge,
                    ProfitMarginPercent = product.ProfitMarginPercent,
                    Cgstpercent = product.Cgstpercent,
                    Sgstpercent = product.Sgstpercent,
                    Igstpercent = product.Igstpercent
                });

                // The unit now belongs to this order: take it out of the available pool.
                sku.SkustatusId = OrderedSkuStatusId;

                _context.Skuhistories.Add(new Skuhistory
                {
                    Sku = sku.Sku1,
                    InventoryId = sku.InventoryId,
                    SkustatusId = OrderedSkuStatusId,
                    HistoryDate = skuHistoryDate
                });
            }

            reservedItems.Add(item);
        }
        await _context.SaveChangesAsync();

        // The units are written, so each line's own books can be opened straight away: the output GST inside what
        // the customer paid for them is already known - the checkout charged it - while the gateway's charge and the
        // couriers' bills are not yet, which is exactly why those stay NULL here until something reports them (see
        // OrderItemMoneyWriter). It is written at placement rather than only at capture so that a sale whose payment
        // is still to come, or one that never is (a retried attempt, cash on delivery), still has its per-line books
        // from the moment the order exists.
        await OrderItemMoneyWriter.RefreshAsync(_context, order.OrderId);

        if (stockChangedItems.Any())
        {
            messages.Add($"Stock changed while placing your order - adjusted for: {string.Join(", ", stockChangedItems)}");
        }

        // Charge only for the lines that actually got stock.
        cartResponse.Items = reservedItems;
        cartResponse.Calculation = RecalculateCart(reservedItems);

        // Add order history
        var history = new OrderHistory
        {
            OrderId = order.OrderId,
            HistoryDate = DateTime.UtcNow,
            OrderStatusId = 1,
            Comments = "Order placed"
        };
        _context.OrderHistories.Add(history);

        // Clear the cart (remaining items after removals)
        if (request.IsGuest)
        {
            var guestCart = await _context.Set<GuestCart>()
                .Include(gc => gc.GuestCartItems)
                .FirstOrDefaultAsync(gc => gc.CustomerId == request.CustomerID);
            if (guestCart != null)
            {
                _context.Set<GuestCartItem>().RemoveRange(guestCart.GuestCartItems);
            }
        }
        else
        {
            var cart = await _context.Carts
                .Include(c => c.CartItems)
                .FirstOrDefaultAsync(c => c.CustomerId == request.CustomerID);
            if (cart != null)
            {
                _context.CartItems.RemoveRange(cart.CartItems);
            }
        }
        await _context.SaveChangesAsync();

        // Create the Razorpay order through their REST API (no official .NET SDK is referenced).
        //
        // The amount is what the cart is worth, and the cart's lines are priced the way the listings are
        // (see OrderService.CartMapping): one unit of each product with everything the product form's
        // 'Pricing & Charges' and 'Taxes' sections say included, times the units taken. So the figure the
        // customer is shown on the product, the total in the cart and the money the gateway is asked for are
        // the same money.
        var totalAmount = cartResponse.Calculation.GrandTotal;
        var amountInPaise = (int)(totalAmount * 100);

        // Generate a unique receipt number
        var receipt = $"HC{order.OrderId:D6}";

        var (razorpayOrderId, razorpayError) = await CreateRazorpayOrder(
            _razorpayKeyId, _razorpayKeySecret, amountInPaise, receipt, order.OrderId);

        if (string.IsNullOrEmpty(razorpayOrderId))
        {
            // Result = 0 rolls the whole transaction back: no stock is consumed and no unpaid
            // order is left behind.
            _logger.LogError("Razorpay order creation failed for receipt {Receipt}: {Error}", receipt, razorpayError);

            return new CreateOrderResponse
            {
                Result = 0,
                Messages = new[] { "We could not start the payment. You have not been charged - please try again." }
            };
        }

        // The attempt is written down before the browser is told about it: this row is what 'My Orders'
        // shows, what 'Pay now' retries and what the refund is issued against.
        await RecordPaymentAttemptAsync(order, razorpayOrderId, amountInPaise, DateTime.UtcNow);

        return new CreateOrderResponse
        {
            Result = 1,
            Messages = messages.ToArray(),
            OrderId = order.OrderId,
            OrderNumber = receipt,
            Amount = totalAmount,
            RazorpayOrderId = razorpayOrderId,
            RazorpayKey = _razorpayKeyId,
            RemovedItems = removedItems.ToArray(),
            AdjustedItems = adjustedItems.ToArray()
        };
    }

    /// <summary>
    /// The signed-in customer's order history for the 'My Orders' page: status, payment state, the
    /// units that were bought (with the product image) and where the order is being shipped.
    /// </summary>
    public async Task<List<OrderListDto>> GetOrdersAsync(long customerId, bool isGuest)
    {
        var orders = await _context.Orders
            .Where(o => o.CustomerId == customerId)
            .Include(o => o.OrderItems)
            .Include(o => o.OrderStatus)
            .Include(o => o.ShippingAddress)
            .Include(o => o.BillingAddress)
            .Include(o => o.OrderHistories)

            // The legs of the order, because the return window is counted from the day the parcel reached
            // the customer - the forward leg's own delivery date (see OrderReturnFlow.DeliveredOn).
            .Include(o => o.OrderShipments)
            .OrderByDescending(o => o.OrderDate)
            .AsNoTracking()
            .ToListAsync();

        if (orders.Count == 0)
            return new List<OrderListDto>();

        // OrderItems only carry the product name, so the product id and image are resolved once for
        // every name in the history - the image is what makes an order list readable.
        var productNames = orders
            .SelectMany(o => o.OrderItems.Select(oi => oi.ProductName))
            .Distinct()
            .ToList();

        var productInfo = await _context.Products
            .Where(p => productNames.Contains(p.ProductName))
            .Select(p => new
            {
                p.ProductName,
                p.ProductId,
                Image = p.ProductImages
                    .Where(pi => pi.IsPromoImage && pi.IsActive)
                    .Select(pi => pi.ImageUrl)
                    .FirstOrDefault() ?? ""
            })
            .AsNoTracking()
            .ToListAsync();

        var productsByName = productInfo
            .GroupBy(p => p.ProductName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        // Status names come from the lookup table instead of hard-coded ids, so a new lifecycle step
        // added by the shop team shows up here too.
        var statusNames = await _context.OrderStatuses
            .AsNoTracking()
            .ToDictionaryAsync(s => s.OrderStatusId, s => s.Status);

        // The money side: a cancelled paid order is waiting for its refund, and that is what 'My
        // Orders' has to say about it. One read for every order (the newest payment row each), rather
        // than an Include on the order query - only the refund state is used here.
        var orderIds = orders.Select(o => o.OrderId).ToList();

        var latestPayments = (await _context.OrderPayments
                .Where(p => orderIds.Contains(p.OrderId))
                .AsNoTracking()
                .ToListAsync())
            .GroupBy(p => p.OrderId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(p => p.PaymentId).First());

        // The parcel of each order, in one query for the whole history: 'My Orders' shows it next to the
        // order's own status. This is what was written down the last time the courier was asked - the
        // page's own pull (RefreshShipmentsAsync) is what asks, so opening the history never waits for a
        // courier and never fails because one is unreachable.
        var shipments = await _shipmentTracking.GetForOrdersAsync(orderIds);

        // The parcels coming back, in the same one query: once a return has been approved the customer is waiting
        // on the pickup, and 'My Orders' follows it exactly like the parcel that went out. Only the orders that
        // have such a leg are in the answer, so an order with no return is simply shown without one.
        var returnShipments = await _shipmentTracking.GetReverseForOrdersAsync(orderIds);

        // The returns of these orders, in one read for the whole history: 'My Orders' shows the customer
        // where a return has got to and - while nothing has been answered yet - offers to take it back.
        // Every row is read, not only the open ones, because a refused or closed return is still what
        // belongs next to the order; the newest row per order is the one that counts (only one can ever be
        // open at a time - see OrderReturnFlow.FindOpenAsync).
        var returns = (await _context.OrderReturns
                .Where(r => orderIds.Contains(r.OrderId))
                .AsNoTracking()
                .ToListAsync())
            .GroupBy(r => r.OrderId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.ReturnId).First());

        // One 'now' for the whole page, so every order is judged against the same moment and a page cannot
        // show one order's window as open and another's as closed by a few milliseconds.
        var now = DateTime.UtcNow;

        return orders.Select(o =>
        {
            var statusName = o.OrderStatus?.Status ?? statusNames.GetValueOrDefault(o.OrderStatusId, "Pending");
            var isPaid = OrderStatusFlow.IsPaid(o.OrderStatusId);

            // Money we are still holding on an order that will not be delivered (or whose refund Razorpay
            // refused) is owed back to the customer: the shop team approves it from the admin order screen -
            // HC.Business.RazorpayRefunds tells the whole story. A returned order counts exactly like a cancelled
            // one (see OrderPaymentStatus.IsRefundOwed, which owns the rule).
            var payment = latestPayments.GetValueOrDefault(o.OrderId);
            var refundPending = payment != null && OrderPaymentStatus.IsRefundOwed(o.OrderStatusId, payment.Status);

            // The order's own return, if it ever had one. The newest row carries everything the customer
            // needs to read: where it has got to, whether anything can still be done about it, and - while it
            // is open - that asking again is pointless. OrderReturnFlow owns the rules this mirrors.
            var latestReturn = returns.GetValueOrDefault(o.OrderId);
            var openReturn = latestReturn != null && OrderReturnStatus.IsOpen(latestReturn.Status)
                ? latestReturn
                : null;

            return new OrderListDto
            {
                OrderId = o.OrderId,
                OrderNumber = $"HC{o.OrderId:D6}",
                OrderDate = o.OrderDate,
                TotalAmount = ProductPricing.ChargedTotal(o.OrderItems),
                StatusId = o.OrderStatusId,
                Status = statusName,
                IsPaid = isPaid,
                RefundPending = refundPending,

                // The refund is what matters to the customer once a paid order is cancelled: the money
                // is on its way back ('Refund pending' until the shop team approves it, 'Refunded' once
                // it has gone), and only an order that was never charged reads 'Not charged'.
                PaymentStatus = refundPending
                    ? "Refund pending"
                    : payment?.Status == OrderPaymentStatus.Refunded
                        ? "Refunded"
                        : isPaid
                            ? "Paid"
                            : o.OrderStatusId == OrderStatusCancelled ? "Not charged" : "Payment pending",

                // A pending order is the only one that can still be paid for: confirmed and beyond is
                // already paid, and a cancelled order puts its units back on the shelf.
                CanPay = OrderStatusFlow.CanCustomerPay(o.OrderStatusId),

                // The customer may cancel while the order is still in the shop's hands - Pending (nothing
                // has been paid) or Confirmed (paid, and the money is given back). From Shipped onwards
                // the parcel is on its way, so 'My Orders' stops offering it and the server refuses it.
                CanCancel = OrderStatusFlow.CanCustomerCancel(o.OrderStatusId),

                // The customer may ask for a return on exactly what the server accepts: delivered, nothing
                // already waiting for an answer and inside the window (OrderReturnFlow.CanCustomerAsk). The
                // window's last day travels with it, so "why can I not return this?" is answered on the page
                // instead of after a failed call.
                CanReturn = OrderReturnFlow.CanCustomerAsk(o, openReturn, _returnWindowDays, now),
                CanWithdrawReturn = openReturn != null && OrderReturnStatus.CanWithdraw(openReturn.Status),
                ReturnStatus = latestReturn?.Status,
                ReturnReason = latestReturn?.Reason,
                ReturnOrigin = latestReturn?.Origin,
                ReturnRequestedOn = latestReturn?.RequestedOn,
                ReturnWindowEndsOn = OrderReturnFlow.WindowEndsOn(o, _returnWindowDays),
                ItemCount = o.OrderItems.Count,

                ShippingAddress = MapAddress(o.ShippingAddress),
                BillingAddress = MapAddress(o.BillingAddress),

                // The parcel the shop recorded for this order, if any - the customer's 'Track parcel' reads
                // this, and the throttled pull replaces it with what the courier says right now.
                Shipment = shipments.GetValueOrDefault(o.OrderId),

                // The parcel coming back, once the return has been approved: the customer follows what they sent
                // back from here ('On its way back', 'Back with us').
                ReturnShipment = returnShipments.GetValueOrDefault(o.OrderId),

                // OrderItems holds one row per physical unit, so equal units are grouped for display.
                Items = o.OrderItems
                    .GroupBy(oi => oi.ProductName, StringComparer.OrdinalIgnoreCase)
                    .Select(g => new OrderItemDto
                    {
                        ProductId = productsByName.TryGetValue(g.Key, out var info) ? info.ProductId : 0,
                        Image = productsByName.TryGetValue(g.Key, out var imageInfo) ? imageInfo.Image : "",
                        ProductName = g.Key,
                        ProductTitle = g.First().ProductTitle,
                        Quantity = g.Count(),
                        Price = ProductPricing.ChargedPrice(g.First())
                    })
                    .ToList(),

                History = o.OrderHistories
                    .OrderBy(h => h.HistoryDate)
                    .Select(h => new OrderHistoryDto
                    {
                        Date = h.HistoryDate,
                        Status = statusNames.GetValueOrDefault(h.OrderStatusId, ""),
                        Comments = h.Comments ?? ""
                    })
                    .ToList()
            };
        }).ToList();
    }

    /// <summary>Longest values the CustomerAddresses columns accept (see 'HC.Data/HomecutiesDbContext.cs').</summary>
    private const int AddressLineMaxLength = 150;
    private const int AddressTitleMaxLength = 20;
    private const int AddressCityStateMaxLength = 50;
    private const int AddressZipcodeMaxLength = 10;
    private const int AddressMobileMaxLength = 12;
    private const int AddressEmailMaxLength = 150;

    /// <summary>Label of an address the customer typed at checkout without naming it.</summary>
    private const string DefaultShippingAddressTitle = "Home";

    /// <summary>Label of a billing address that is not the shipping address (see above).</summary>
    private const string DefaultBillingAddressTitle = "Billing";

    /// <summary>The only country the shop delivers to - the column is NOT NULL.</summary>
    private const string DefaultAddressCountry = "India";

    /// <summary>
    /// Checks the addresses typed at checkout against the CustomerAddresses columns. A saved address
    /// (ShippingAddressId / BillingAddressId) is already stored in those columns, so only the typed
    /// fields need looking at. Returns the first problem found, or null when everything fits.
    /// </summary>
    private static string? ValidateTypedAddresses(CreateOrderRequest request)
    {
        if (request.ShippingAddressId == 0)
        {
            var shippingProblem = AddressLengthProblem(
                request.ShippingAddress,
                request.ShippingAddressLine2,
                request.ShippingAddressTitle,
                request.City,
                request.State,
                request.ZipCode,
                request.PhoneNumber,
                request.Email);

            if (shippingProblem != null)
            {
                return shippingProblem;
            }
        }

        if (!request.BillingSameAsShipping && request.BillingAddressId == 0)
        {
            return AddressLengthProblem(
                request.BillingAddressLine1,
                request.BillingAddressLine2,
                request.BillingContactName,
                request.BillingCity,
                request.BillingState,
                request.BillingZipCode,
                request.BillingPhoneNumber,
                request.BillingEmail);
        }

        return null;
    }

    private static string? AddressLengthProblem(
        string? line1,
        string? line2,
        string? title,
        string? city,
        string? state,
        string? zipcode,
        string? mobile,
        string? email)
    {
        if ((line1 ?? "").Trim().Length > AddressLineMaxLength || (line2 ?? "").Trim().Length > AddressLineMaxLength)
            return $"Please keep each address line under {AddressLineMaxLength} characters.";

        if ((title ?? "").Trim().Length > AddressTitleMaxLength)
            return $"Please keep the address label under {AddressTitleMaxLength} characters.";

        if ((city ?? "").Trim().Length > AddressCityStateMaxLength ||
            (state ?? "").Trim().Length > AddressCityStateMaxLength)
            return $"Please keep the city and state under {AddressCityStateMaxLength} characters.";

        if ((zipcode ?? "").Trim().Length > AddressZipcodeMaxLength)
            return "Please check the PIN code - it looks too long.";

        if ((mobile ?? "").Trim().Length > AddressMobileMaxLength)
            return "Please check the phone number - it looks too long.";

        if ((email ?? "").Trim().Length > AddressEmailMaxLength)
            return "Please check the email address - it looks too long.";

        return null;
    }

    /// <summary>
    /// The address the order is delivered to. An address picked from the customer's address book
    /// (ShippingAddressId) is used as it stands: the id is matched against the customer first, so a
    /// guessed id can never ship an order to somebody else's saved address, and no duplicate row is
    /// written for an address that already exists. An address typed at checkout is stored in the
    /// customer's address book - that is what the flat shipping fields have always done - so it can
    /// be picked for the next order instead of being typed again; when the book already holds that
    /// delivery address (see <see cref="FindMatchingAddressAsync"/>) the row that is there is used,
    /// so typing the same address again does not add a second copy of it. Null means there is no
    /// usable address at all.
    /// </summary>
    private async Task<CustomerAddress?> ResolveShippingAddressAsync(long customerId, CreateOrderRequest request)
    {
        if (request.ShippingAddressId > 0)
        {
            var saved = await FindSavedAddressAsync(customerId, request.ShippingAddressId);
            if (saved != null)
            {
                return saved;
            }
        }

        var line1 = (request.ShippingAddress ?? "").Trim();
        var city = (request.City ?? "").Trim();
        var state = (request.State ?? "").Trim();
        var zipcode = (request.ZipCode ?? "").Trim();

        if (line1.Length == 0 || city.Length == 0 || state.Length == 0 || zipcode.Length == 0)
        {
            return null;
        }

        var line2 = SecondLineOrNull(request.ShippingAddressLine2);
        var mobile = (request.PhoneNumber ?? "").Trim();
        var typedContactName = (request.ShippingContactName ?? "").Trim();

        var known = await FindMatchingAddressAsync(customerId, line1, line2, city, state, zipcode, mobile, typedContactName);
        if (known != null)
        {
            return known;
        }

        var contactName = typedContactName;
        if (contactName.Length == 0 && !request.IsGuest)
        {
            contactName = await GetCustomerContactNameAsync(customerId);
        }

        var address = new CustomerAddress
        {
            CustomerId = customerId,
            AddressTitle = TitleOr(request.ShippingAddressTitle, DefaultShippingAddressTitle),
            AddressLine1 = line1,
            AddressLine2 = line2,
            City = city,
            State = state,
            Zipcode = zipcode,
            Country = DefaultAddressCountry,
            MobileNumber = mobile,
            EmailId = (request.Email ?? "").Trim(),
            ContactName = contactName
        };

        _context.CustomerAddresses.Add(address);
        await _context.SaveChangesAsync();

        return address;
    }

    /// <summary>
    /// The address the order is invoiced to. It is the shipping address unless the customer asked for
    /// a different one (BillingSameAsShipping = false); that different one is either picked from the
    /// address book (BillingAddressId, checked against the customer like the shipping id) or typed in
    /// checkout, in which case it is stored in the address book too - unless the book already holds
    /// that billing address, in which case the row that is there is used (see
    /// <see cref="FindMatchingAddressAsync"/>). Null means the billing address the customer gave is not
    /// usable - the order is refused rather than silently billed to the shipping address.
    /// </summary>
    private async Task<CustomerAddress?> ResolveBillingAddressAsync(
        long customerId,
        CreateOrderRequest request,
        CustomerAddress shippingAddress)
    {
        if (request.BillingSameAsShipping)
        {
            return shippingAddress;
        }

        if (request.BillingAddressId > 0)
        {
            var saved = await FindSavedAddressAsync(customerId, request.BillingAddressId);
            if (saved != null)
            {
                return saved;
            }
        }

        var line1 = (request.BillingAddressLine1 ?? "").Trim();
        var city = (request.BillingCity ?? "").Trim();
        var state = (request.BillingState ?? "").Trim();
        var zipcode = (request.BillingZipCode ?? "").Trim();

        if (line1.Length == 0 || city.Length == 0 || state.Length == 0 || zipcode.Length == 0)
        {
            return null;
        }

        var line2 = SecondLineOrNull(request.BillingAddressLine2);
        var mobile = (request.BillingPhoneNumber ?? "").Trim();
        var typedContactName = (request.BillingContactName ?? "").Trim();

        var known = await FindMatchingAddressAsync(customerId, line1, line2, city, state, zipcode, mobile, typedContactName);
        if (known != null)
        {
            return known;
        }

        var contactName = typedContactName;
        if (contactName.Length == 0 && !request.IsGuest)
        {
            contactName = await GetCustomerContactNameAsync(customerId);
        }

        var billing = new CustomerAddress
        {
            CustomerId = customerId,
            AddressTitle = DefaultBillingAddressTitle,
            AddressLine1 = line1,
            AddressLine2 = line2,
            City = city,
            State = state,
            Zipcode = zipcode,
            Country = DefaultAddressCountry,
            MobileNumber = mobile,
            EmailId = (request.BillingEmail ?? "").Trim(),
            ContactName = contactName
        };

        _context.CustomerAddresses.Add(billing);
        await _context.SaveChangesAsync();

        return billing;
    }

    /// <summary>
    /// One saved address of this customer. The customer id is part of the lookup on purpose: an
    /// address id arriving from the browser must never resolve to somebody else's address.
    /// </summary>
    private async Task<CustomerAddress?> FindSavedAddressAsync(long customerId, long addressId)
    {
        return await _context.CustomerAddresses
            .FirstOrDefaultAsync(a => a.AddressId == addressId && a.CustomerId == customerId);
    }

    /// <summary>
    /// The row in this customer's address book that is already that delivery address, or null when the
    /// customer has not stored it before. An address typed at checkout used to be written as a fresh row
    /// every single time, so 'My Addresses' filled up with one copy of the same address per order placed
    /// with it - and each copy then refuses to be deleted, because the order it was written for points
    /// at it. The row that is already there is used instead, exactly as it stands: nothing on it is
    /// rewritten, so an earlier order keeps showing the address and the contact details it was placed
    /// with.
    ///
    /// The comparison is on the details that decide where the parcel goes and who is called about it -
    /// both address lines, city, state, PIN and mobile number - ignoring case and surrounding spaces,
    /// because those are what a re-typed address differs by in practice ("Bangalore" vs "bangalore", a
    /// PIN typed with a stray space). The label ('Shipping', 'Home') and the e-mail are not part of it:
    /// they do not change where the order goes, so re-typing a different label or e-mail is still the
    /// same address.
    ///
    /// A recipient the customer named is part of it - somebody else receiving the parcel is a different
    /// delivery and gets its own row. When they name nobody, the row kept for that address is used as
    /// it stands: the customer's own name that was filled in for an earlier order is not a different
    /// delivery, and treating it as one is exactly how the second copy used to appear.
    /// </summary>
    private async Task<CustomerAddress?> FindMatchingAddressAsync(
        long customerId,
        string line1,
        string? line2,
        string city,
        string state,
        string zipcode,
        string mobile,
        string contactName)
    {
        var addressLine1 = line1.Trim().ToLower();
        var addressLine2 = (line2 ?? "").Trim().ToLower();
        var addressCity = city.Trim().ToLower();
        var addressState = state.Trim().ToLower();
        var addressZipcode = zipcode.Trim().ToLower();
        var addressMobile = mobile.Trim().ToLower();

        var query = _context.CustomerAddresses
            .Where(a => a.CustomerId == customerId)
            .Where(a =>
                a.AddressLine1.ToLower() == addressLine1 &&
                (a.AddressLine2 ?? "").ToLower() == addressLine2 &&
                a.City.ToLower() == addressCity &&
                a.State.ToLower() == addressState &&
                a.Zipcode.ToLower() == addressZipcode &&
                a.MobileNumber.ToLower() == addressMobile);

        var typedContactName = contactName.Trim().ToLower();
        if (typedContactName.Length > 0)
        {
            query = query.Where(a => (a.ContactName ?? "").ToLower() == typedContactName);
        }

        return await query.FirstOrDefaultAsync();
    }

    /// <summary>The customer's own name - the contact name of a newly stored address when none is given.</summary>
    private async Task<string> GetCustomerContactNameAsync(long customerId)
    {
        var name = await _context.Customers
            .AsNoTracking()
            .Where(c => c.CustomerId == customerId)
            .Select(c => new { c.FirstName, c.LastName })
            .FirstOrDefaultAsync();

        return name == null ? "" : $"{name.FirstName} {name.LastName}".Trim();
    }

    private static string TitleOr(string? title, string fallback)
    {
        var trimmed = (title ?? "").Trim();

        return trimmed.Length == 0 ? fallback : trimmed;
    }

    private static string? SecondLineOrNull(string? secondLine)
    {
        var trimmed = (secondLine ?? "").Trim();

        return trimmed.Length == 0 ? null : trimmed;
    }

    /// <summary>
    /// An order's address as 'My Orders' shows it. The id is kept so the storefront can tell a billing
    /// address apart from the shipping one and only show it when the customer asked for a different one.
    /// </summary>
    private static OrderAddressDto MapAddress(CustomerAddress? address)
    {
        if (address == null)
        {
            return new OrderAddressDto();
        }

        return new OrderAddressDto
        {
            AddressId = address.AddressId,
            ContactName = address.ContactName,
            AddressLine1 = address.AddressLine1,
            AddressLine2 = address.AddressLine2 ?? "",
            City = address.City,
            State = address.State,
            Zipcode = address.Zipcode,
            MobileNumber = address.MobileNumber,
            EmailId = address.EmailId
        };
    }

    /// <summary>
    /// Guests are stored in GuestCustomers, but Orders/CustomerAddresses reference Customers.
    /// Finds an existing customer by e-mail or mobile number (so repeat guest orders don't create
    /// duplicates) and creates one when there is none, then returns the Customers.CustomerID to
    /// use for the order and its address. Runs inside the caller's transaction.
    /// </summary>
    private async Task<long> ResolveGuestCustomerIdAsync(CreateOrderRequest request)
    {
        var email = (request.Email ?? "").Trim();
        var mobile = (request.PhoneNumber ?? "").Trim();

        var existing = await _context.Customers.FirstOrDefaultAsync(c =>
            (!string.IsNullOrEmpty(email) && c.EmailId == email) ||
            (!string.IsNullOrEmpty(mobile) && c.MobileNumber == mobile));

        if (existing != null)
        {
            return existing.CustomerId;
        }

        var now = DateTime.UtcNow;
        var customer = new Customer
        {
            FirstName = "Guest",
            EmailId = string.IsNullOrEmpty(email) ? $"guest_{Guid.NewGuid():N}@homecuties.local" : email,
            MobileNumber = string.IsNullOrEmpty(mobile) ? null : mobile,
            EmailVerfied = false,
            MobileVerified = false,
            CustomerStatusId = 1, // 1 = ACTIVE
            CreatedOn = now,
            ModifiedOn = now
        };

        _context.Customers.Add(customer);
        await _context.SaveChangesAsync();

        return customer.CustomerId;
    }

    /// <summary>
    /// Locks and returns up to <paramref name="quantity"/> sellable SKUs for a product.
    /// The rows are taken with UPDLOCK/ROWLOCK/HOLDLOCK, so the locks are held until the
    /// surrounding transaction commits or rolls back. A concurrent checkout asking for the same
    /// units will block here and then see them as taken - it can never sell the same unit twice.
    /// </summary>
    private async Task<List<Sku>> LockAvailableSkusAsync(int productId, int quantity)
    {
        // A unit is sellable while it is in the "Available" pool and the order holding it - if any -
        // was cancelled (that is when the item went back on the shelf). {3} is that cancelled status.
        const string sql = @"
SELECT TOP ({0}) * FROM [SKUs] AS s WITH (UPDLOCK, ROWLOCK, HOLDLOCK)
WHERE s.[SKUStatusID] = {2}
  AND s.[PurchaseDetailID] IN (SELECT pd.[PurchaseDetailID] FROM [PurchaseDetails] AS pd WHERE pd.[ProductID] = {1})
  AND NOT EXISTS (
      SELECT 1 FROM [OrderItems] AS oi
      INNER JOIN [Orders] AS o ON o.[OrderID] = oi.[OrderID]
      WHERE oi.[SKU] = s.[SKU] AND o.[OrderStatusID] <> {3})
ORDER BY s.[SKU]";

        return await _context.Skus
            .FromSqlRaw(sql, quantity, productId, AvailableSkuStatusId, SkuAvailability.CancelledOrderStatusId)
            .AsTracking()
            .ToListAsync();
    }


}
