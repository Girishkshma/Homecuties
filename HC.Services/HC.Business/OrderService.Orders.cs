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

        // Create customer address
        var address = new CustomerAddress
        {
            CustomerId = orderCustomerId,
            AddressTitle = "Shipping",
            AddressLine1 = request.ShippingAddress,
            City = request.City,
            State = request.State,
            Zipcode = request.ZipCode,
            Country = "India",
            MobileNumber = request.PhoneNumber,
            EmailId = request.Email,
            ContactName = ""
        };
        _context.CustomerAddresses.Add(address);
        await _context.SaveChangesAsync();

        // Create the order
        var order = new Order
        {
            CustomerId = orderCustomerId,
            SellerId = 1, // Default seller
            OrderDate = DateTime.UtcNow,
            BillingAddressId = address.AddressId,
            ShippingAddressId = address.AddressId,
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
            .Include(o => o.OrderHistories)
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

        return orders.Select(o =>
        {
            var statusName = o.OrderStatus?.Status ?? statusNames.GetValueOrDefault(o.OrderStatusId, "Pending");
            var isPaid = o.OrderStatusId is OrderStatusConfirmed or OrderStatusShipped or OrderStatusDelivered;

            return new OrderListDto
            {
                OrderId = o.OrderId,
                OrderNumber = $"HC{o.OrderId:D6}",
                OrderDate = o.OrderDate,
                TotalAmount = o.OrderItems.Sum(oi => oi.UnitPrice),
                StatusId = o.OrderStatusId,
                Status = statusName,
                IsPaid = isPaid,
                PaymentStatus = isPaid
                    ? "Paid"
                    : o.OrderStatusId == OrderStatusCancelled ? "Not charged" : "Payment pending",

                // Only an order that has not been paid yet can be cancelled by the customer; cancelling
                // a paid order would need a refund (handled by the shop team).
                CanCancel = o.OrderStatusId == OrderStatusPending,
                ItemCount = o.OrderItems.Count,

                ShippingAddress = o.ShippingAddress == null
                    ? new OrderAddressDto()
                    : new OrderAddressDto
                    {
                        ContactName = o.ShippingAddress.ContactName,
                        AddressLine1 = o.ShippingAddress.AddressLine1,
                        AddressLine2 = o.ShippingAddress.AddressLine2 ?? "",
                        City = o.ShippingAddress.City,
                        State = o.ShippingAddress.State,
                        Zipcode = o.ShippingAddress.Zipcode,
                        MobileNumber = o.ShippingAddress.MobileNumber,
                        EmailId = o.ShippingAddress.EmailId
                    },

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
                        Price = g.First().UnitPrice
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
