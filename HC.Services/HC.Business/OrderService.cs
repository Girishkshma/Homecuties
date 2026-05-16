using System.Security.Cryptography;
using System.Text;
using HC.Business.Dtos;
using HC.Data;
using HC.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace HC.Business;

public class OrderService : IOrderService
{
    private readonly HomecutiesDbContext _context;
    private readonly IConfiguration _configuration;

    public OrderService(HomecutiesDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    public async Task<CreateOrderResponse> CreateOrderAsync(CreateOrderRequest request)
    {
        // Get the cart items
        CartResponseDto cartResponse;
        if (request.IsGuest)
        {
            var guestCart = await _context.Set<GuestCart>()
                .Include(gc => gc.GuestCartItems)
                    .ThenInclude(ci => ci.Product)
                        .ThenInclude(p => p.ProductImages)
                .Include(gc => gc.GuestCartItems)
                    .ThenInclude(ci => ci.Product)
                        .ThenInclude(p => p.PurchaseDetails)
                            .ThenInclude(pd => pd.Skus)
                .FirstOrDefaultAsync(gc => gc.CustomerId == request.CustomerID);

            if (guestCart == null || !guestCart.GuestCartItems.Any())
                return new CreateOrderResponse { Result = 0, Messages = new[] { "Cart is empty" } };

            cartResponse = MapGuestCartToResponse(guestCart);
        }
        else
        {
            var cart = await _context.Carts
                .Include(c => c.CartItems)
                    .ThenInclude(ci => ci.Product)
                        .ThenInclude(p => p.ProductImages)
                .Include(c => c.CartItems)
                    .ThenInclude(ci => ci.Product)
                        .ThenInclude(p => p.PurchaseDetails)
                            .ThenInclude(pd => pd.Skus)
                .FirstOrDefaultAsync(c => c.CustomerId == request.CustomerID);

            if (cart == null || !cart.CartItems.Any())
                return new CreateOrderResponse { Result = 0, Messages = new[] { "Cart is empty" } };

            cartResponse = MapCartToResponse(cart);
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

        // Create customer address
        var address = new CustomerAddress
        {
            CustomerId = request.CustomerID,
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
            CustomerId = request.CustomerID,
            SellerId = 1, // Default seller
            OrderDate = DateTime.UtcNow,
            BillingAddressId = address.AddressId,
            ShippingAddressId = address.AddressId,
            OrderStatusId = 1 // Pending
        };
        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        // Create order items and reserve SKUs
        foreach (var item in validItems)
        {
            // Get available SKUs for this product
            var product = await _context.Products
                .Include(p => p.PurchaseDetails)
                    .ThenInclude(pd => pd.Skus)
                .FirstOrDefaultAsync(p => p.ProductId == item.ProductID);

            if (product == null) continue;

            var availableSkus = product.PurchaseDetails
                .SelectMany(pd => pd.Skus)
                .Where(s => !s.OrderItems.Any())
                .Take(item.Quantity)
                .ToList();

            foreach (var sku in availableSkus)
            {
                var orderItem = new OrderItem
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
                };
                _context.OrderItems.Add(orderItem);
            }
        }
        await _context.SaveChangesAsync();

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

        // Create Razorpay order
        var razorpayKey = _configuration["Razorpay:KeyId"] ?? "rzp_test_placeholder";
        var razorpaySecret = _configuration["Razorpay:KeySecret"] ?? "test_secret";
        var totalAmount = cartResponse.Calculation.GrandTotal;
        var amountInPaise = (int)(totalAmount * 100);

        // Generate a unique receipt number
        var receipt = $"HC{order.OrderId:D6}";

        // For Razorpay, we need to create an order via their API
        // Since we don't have the Razorpay .NET SDK installed, we'll use HttpClient
        var razorpayOrderId = await CreateRazorpayOrder(razorpayKey, razorpaySecret, amountInPaise, receipt);

        return new CreateOrderResponse
        {
            Result = 1,
            Messages = messages.ToArray(),
            OrderId = order.OrderId,
            OrderNumber = receipt,
            Amount = totalAmount,
            RazorpayOrderId = razorpayOrderId,
            RazorpayKey = razorpayKey,
            RemovedItems = removedItems.ToArray(),
            AdjustedItems = adjustedItems.ToArray()
        };
    }

    public async Task<ResultDto> VerifyPaymentAsync(VerifyPaymentRequest request)
    {
        var razorpaySecret = _configuration["Razorpay:KeySecret"] ?? "test_secret";

        // Verify the payment signature
        var expectedSignature = GenerateRazorpaySignature(
            request.RazorpayOrderId,
            request.RazorpayPaymentId,
            razorpaySecret);

        if (expectedSignature != request.RazorpaySignature)
        {
            return new ResultDto { Result = 0, Messages = new[] { "Payment verification failed - invalid signature" } };
        }

        // Update order status
        var order = await _context.Orders.FindAsync(request.OrderId);
        if (order == null)
            return new ResultDto { Result = 0, Messages = new[] { "Order not found" } };

        order.OrderStatusId = 2; // Confirmed/Processing

        var history = new OrderHistory
        {
            OrderId = order.OrderId,
            HistoryDate = DateTime.UtcNow,
            OrderStatusId = 2,
            Comments = $"Payment received. Razorpay Payment ID: {request.RazorpayPaymentId}"
        };
        _context.OrderHistories.Add(history);
        await _context.SaveChangesAsync();

        return new ResultDto { Result = 1, Messages = new[] { "Payment verified successfully" } };
    }

    public async Task<List<OrderListDto>> GetOrdersAsync(long customerId, bool isGuest)
    {
        var orders = await _context.Orders
            .Where(o => o.CustomerId == customerId)
            .Include(o => o.OrderItems)
            .Include(o => o.OrderStatus)
            .OrderByDescending(o => o.OrderDate)
            .ToListAsync();

        return orders.Select(o => new OrderListDto
        {
            OrderId = o.OrderId,
            OrderNumber = $"HC{o.OrderId:D6}",
            OrderDate = o.OrderDate,
            TotalAmount = o.OrderItems.Sum(oi => oi.UnitPrice),
            Status = o.OrderStatus?.Status ?? "Pending",
            PaymentStatus = o.OrderStatusId >= 2 ? "Paid" : "Pending",
            Items = o.OrderItems.GroupBy(oi => oi.ProductName).Select(g => new OrderItemDto
            {
                ProductId = 0,
                ProductName = g.Key,
                ProductTitle = g.First().ProductTitle,
                Quantity = g.Count(),
                Price = g.First().UnitPrice,
                Image = ""
            }).ToList()
        }).ToList();
    }

    private static CartResponseDto MapGuestCartToResponse(GuestCart guestCart)
    {
        var items = guestCart.GuestCartItems.Select(ci =>
        {
            var availableSkus = ci.Product.PurchaseDetails
                .SelectMany(pd => pd.Skus)
                .Where(s => !s.OrderItems.Any())
                .ToList();
            var availableQty = availableSkus.Count;
            return new CartItemDto
            {
                ProductID = ci.ProductId,
                ProductName = ci.Product.ProductName,
                ProductTitle = ci.Product.ProductTitle,
                Quantity = ci.Quantity,
                Price = ci.Product.UnitPrice,
                Image = ci.Product.ProductImages
                    .Where(pi => pi.IsPromoImage && pi.IsActive)
                    .Select(pi => pi.ImageUrl)
                    .FirstOrDefault() ?? "",
                IsInStock = availableQty > 0,
                AvailableQty = availableQty
            };
        }).ToList();

        var calculation = new CartCalculationDto
        {
            Count = items.Sum(i => i.Quantity),
            SalesPrice = items.Sum(i => i.Price * i.Quantity),
            GrandTotal = items.Sum(i => i.Price * i.Quantity)
        };

        return new CartResponseDto { Items = items, Calculation = calculation };
    }

    private static CartResponseDto MapCartToResponse(Data.Entities.Cart cart)
    {
        var items = cart.CartItems.Select(ci =>
        {
            var availableSkus = ci.Product.PurchaseDetails
                .SelectMany(pd => pd.Skus)
                .Where(s => !s.OrderItems.Any())
                .ToList();
            var availableQty = availableSkus.Count;
            return new CartItemDto
            {
                ProductID = ci.ProductId,
                ProductName = ci.Product.ProductName,
                ProductTitle = ci.Product.ProductTitle,
                Quantity = ci.Quantity,
                Price = ci.Product.UnitPrice,
                Image = ci.Product.ProductImages
                    .Where(pi => pi.IsPromoImage && pi.IsActive)
                    .Select(pi => pi.ImageUrl)
                    .FirstOrDefault() ?? "",
                IsInStock = availableQty > 0,
                AvailableQty = availableQty
            };
        }).ToList();

        var calculation = new CartCalculationDto
        {
            Count = items.Sum(i => i.Quantity),
            SalesPrice = items.Sum(i => i.Price * i.Quantity),
            GrandTotal = items.Sum(i => i.Price * i.Quantity)
        };

        return new CartResponseDto { Items = items, Calculation = calculation };
    }

    private static CartCalculationDto RecalculateCart(List<CartItemDto> items)
    {
        return new CartCalculationDto
        {
            Count = items.Sum(i => i.Quantity),
            SalesPrice = items.Sum(i => i.Price * i.Quantity),
            GrandTotal = items.Sum(i => i.Price * i.Quantity)
        };
    }

    private static string GenerateRazorpaySignature(string orderId, string paymentId, string secret)
    {
        var payload = $"{orderId}|{paymentId}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return BitConverter.ToString(hash).Replace("-", "").ToLower();
    }

    private static async Task<string> CreateRazorpayOrder(string keyId, string keySecret, int amountInPaise, string receipt)
    {
        try
        {
            using var client = new HttpClient();
            var auth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{keyId}:{keySecret}"));
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", auth);

            var payload = new
            {
                amount = amountInPaise,
                currency = "INR",
                receipt = receipt,
                payment_capture = 1
            };

            var json = System.Text.Json.JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await client.PostAsync("https://api.razorpay.com/v1/orders", content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var doc = System.Text.Json.JsonDocument.Parse(responseBody);
                return doc.RootElement.GetProperty("id").GetString() ?? "";
            }

            // If Razorpay API call fails (e.g., test keys not configured), generate a mock order ID
            return $"order_mock_{Guid.NewGuid():N}";
        }
        catch
        {
            // Fallback for development/testing
            return $"order_mock_{Guid.NewGuid():N}";
        }
    }
}
