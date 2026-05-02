using HC.Business.Dtos;
using HC.Data;
using HC.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace HC.Business;

public class CartService : ICartService
{
    private readonly HomecutiesDbContext _context;

    public CartService(HomecutiesDbContext context)
    {
        _context = context;
    }

    public async Task<ResultDto> AddToCartAsync(long customerId, bool isGuest, int productId, int quantity)
    {
        if (isGuest)
        {
            // Ensure a GuestCustomer record exists before creating a GuestCart.
            // The GuestCustomer.CustomerId is auto-generated (identity column), so we need
            // to use the generated ID for the GuestCart.
            var guestCustomer = await _context.Set<GuestCustomer>()
                .FirstOrDefaultAsync(gc => gc.CustomerId == customerId);

            if (guestCustomer == null)
            {
                guestCustomer = new GuestCustomer
                {
                    CreatedOn = DateTime.UtcNow
                };
                _context.Set<GuestCustomer>().Add(guestCustomer);
                await _context.SaveChangesAsync();
                // Use the auto-generated CustomerId from the database
                customerId = guestCustomer.CustomerId;
            }

            var guestCart = await _context.Set<GuestCart>()
                .Include(gc => gc.GuestCartItems)
                .FirstOrDefaultAsync(gc => gc.CustomerId == customerId);

            if (guestCart == null)
            {
                guestCart = new GuestCart
                {
                    CustomerId = customerId,
                    CreatedOn = DateTime.UtcNow
                };
                _context.Set<GuestCart>().Add(guestCart);
                await _context.SaveChangesAsync();
            }

            var existingItem = guestCart.GuestCartItems
                .FirstOrDefault(ci => ci.ProductId == productId);

            if (existingItem != null)
            {
                existingItem.Quantity += (short)quantity;
            }
            else
            {
                guestCart.GuestCartItems.Add(new GuestCartItem
                {
                    ProductId = productId,
                    Quantity = (short)quantity
                });
            }
        }
        else
        {
            var cart = await _context.Carts
                .Include(c => c.CartItems)
                .FirstOrDefaultAsync(c => c.CustomerId == customerId);

            if (cart == null)
            {
                cart = new Cart
                {
                    CustomerId = customerId,
                    CreatedOn = DateTime.UtcNow
                };
                _context.Carts.Add(cart);
                await _context.SaveChangesAsync();
            }

            var existingItem = cart.CartItems
                .FirstOrDefault(ci => ci.ProductId == productId);

            if (existingItem != null)
            {
                existingItem.Quantity += (short)quantity;
            }
            else
            {
                cart.CartItems.Add(new CartItem
                {
                    ProductId = productId,
                    Quantity = (short)quantity
                });
            }
        }

        await _context.SaveChangesAsync();
        return new ResultDto { Result = 1, Messages = new[] { "Item added to cart" } };
    }

    public async Task<CartResponseDto> GetCartAsync(long customerId, bool isGuest)
    {
        if (isGuest)
        {
            var guestCart = await _context.Set<GuestCart>()
                .Include(gc => gc.GuestCartItems)
                    .ThenInclude(ci => ci.Product)
                        .ThenInclude(p => p.ProductImages)
                .FirstOrDefaultAsync(gc => gc.CustomerId == customerId);

            if (guestCart == null)
                return new CartResponseDto();

            var cartItems = guestCart.GuestCartItems.Select(ci => new CartItemDto
            {
                ProductID = ci.ProductId,
                ProductName = ci.Product.ProductName,
                ProductTitle = ci.Product.ProductTitle,
                Quantity = ci.Quantity,
                Price = ci.Product.UnitPrice,
                Image = ci.Product.ProductImages
                    .Where(pi => pi.IsPromoImage && pi.IsActive)
                    .Select(pi => pi.ImageUrl)
                    .FirstOrDefault() ?? ""
            }).ToList();

            var calculation = CalculateCart(guestCart.GuestCartItems.ToList());
            return new CartResponseDto { Items = cartItems, Calculation = calculation };
        }
        else
        {
            var cart = await _context.Carts
                .Include(c => c.CartItems)
                    .ThenInclude(ci => ci.Product)
                        .ThenInclude(p => p.ProductImages)
                .FirstOrDefaultAsync(c => c.CustomerId == customerId);

            if (cart == null)
                return new CartResponseDto();

            var cartItems = cart.CartItems.Select(ci => new CartItemDto
            {
                ProductID = ci.ProductId,
                ProductName = ci.Product.ProductName,
                ProductTitle = ci.Product.ProductTitle,
                Quantity = ci.Quantity,
                Price = ci.Product.UnitPrice,
                Image = ci.Product.ProductImages
                    .Where(pi => pi.IsPromoImage && pi.IsActive)
                    .Select(pi => pi.ImageUrl)
                    .FirstOrDefault() ?? ""
            }).ToList();

            var calculation = CalculateCart(cart.CartItems.ToList());
            return new CartResponseDto { Items = cartItems, Calculation = calculation };
        }
    }

    public async Task<CartCalculationDto> GetItemsCountAsync(long customerId, bool isGuest)
    {
        if (isGuest)
        {
            var guestCart = await _context.Set<GuestCart>()
                .Include(gc => gc.GuestCartItems)
                    .ThenInclude(ci => ci.Product)
                .FirstOrDefaultAsync(gc => gc.CustomerId == customerId);

            return CalculateCart(guestCart?.GuestCartItems.ToList() ?? new List<GuestCartItem>());
        }
        else
        {
            var cart = await _context.Carts
                .Include(c => c.CartItems)
                    .ThenInclude(ci => ci.Product)
                .FirstOrDefaultAsync(c => c.CustomerId == customerId);

            return CalculateCart(cart?.CartItems.ToList() ?? new List<CartItem>());
        }
    }

    public async Task<ResultDto> UpdateCartItemQuantityAsync(long customerId, bool isGuest, int productId, int quantity)
    {
        if (isGuest)
        {
            var guestCart = await _context.Set<GuestCart>()
                .Include(gc => gc.GuestCartItems)
                .FirstOrDefaultAsync(gc => gc.CustomerId == customerId);

            if (guestCart == null)
                return new ResultDto { Result = 0, Messages = new[] { "Cart not found" } };

            var item = guestCart.GuestCartItems.FirstOrDefault(ci => ci.ProductId == productId);
            if (item == null)
                return new ResultDto { Result = 0, Messages = new[] { "Item not found in cart" } };

            if (quantity <= 0)
            {
                guestCart.GuestCartItems.Remove(item);
            }
            else
            {
                item.Quantity = (short)quantity;
            }
        }
        else
        {
            var cart = await _context.Carts
                .Include(c => c.CartItems)
                .FirstOrDefaultAsync(c => c.CustomerId == customerId);

            if (cart == null)
                return new ResultDto { Result = 0, Messages = new[] { "Cart not found" } };

            var item = cart.CartItems.FirstOrDefault(ci => ci.ProductId == productId);
            if (item == null)
                return new ResultDto { Result = 0, Messages = new[] { "Item not found in cart" } };

            if (quantity <= 0)
            {
                cart.CartItems.Remove(item);
            }
            else
            {
                item.Quantity = (short)quantity;
            }
        }

        await _context.SaveChangesAsync();
        return new ResultDto { Result = 1, Messages = new[] { "Cart updated" } };
    }

    public async Task<ResultDto> RemoveFromCartAsync(long customerId, bool isGuest, int productId)
    {
        if (isGuest)
        {
            var guestCart = await _context.Set<GuestCart>()
                .Include(gc => gc.GuestCartItems)
                .FirstOrDefaultAsync(gc => gc.CustomerId == customerId);

            if (guestCart == null)
                return new ResultDto { Result = 0, Messages = new[] { "Cart not found" } };

            var item = guestCart.GuestCartItems.FirstOrDefault(ci => ci.ProductId == productId);
            if (item == null)
                return new ResultDto { Result = 0, Messages = new[] { "Item not found in cart" } };

            guestCart.GuestCartItems.Remove(item);
        }
        else
        {
            var cart = await _context.Carts
                .Include(c => c.CartItems)
                .FirstOrDefaultAsync(c => c.CustomerId == customerId);

            if (cart == null)
                return new ResultDto { Result = 0, Messages = new[] { "Cart not found" } };

            var item = cart.CartItems.FirstOrDefault(ci => ci.ProductId == productId);
            if (item == null)
                return new ResultDto { Result = 0, Messages = new[] { "Item not found in cart" } };

            cart.CartItems.Remove(item);
        }

        await _context.SaveChangesAsync();
        return new ResultDto { Result = 1, Messages = new[] { "Item removed from cart" } };
    }

    public async Task<ResultDto> TransferGuestCartAsync(long guestCustomerId, long customerId)
    {
        var guestCart = await _context.Set<GuestCart>()
            .Include(gc => gc.GuestCartItems)
            .FirstOrDefaultAsync(gc => gc.CustomerId == guestCustomerId);

        if (guestCart == null)
            return new ResultDto { Result = 1, Messages = new[] { "No guest cart to transfer" } };

        var cart = await _context.Carts
            .Include(c => c.CartItems)
            .FirstOrDefaultAsync(c => c.CustomerId == customerId);

        if (cart == null)
        {
            cart = new Cart
            {
                CustomerId = customerId,
                CreatedOn = DateTime.UtcNow
            };
            _context.Carts.Add(cart);
            await _context.SaveChangesAsync();
        }

        foreach (var guestItem in guestCart.GuestCartItems)
        {
            var existingItem = cart.CartItems
                .FirstOrDefault(ci => ci.ProductId == guestItem.ProductId);

            if (existingItem != null)
            {
                existingItem.Quantity += guestItem.Quantity;
            }
            else
            {
                cart.CartItems.Add(new CartItem
                {
                    ProductId = guestItem.ProductId,
                    Quantity = guestItem.Quantity
                });
            }
        }

        _context.Set<GuestCart>().Remove(guestCart);
        await _context.SaveChangesAsync();

        return new ResultDto { Result = 1, Messages = new[] { "Cart transferred" } };
    }

    private static CartCalculationDto CalculateCart(List<CartItem> items)
    {
        var count = items.Sum(i => (int)i.Quantity);
        var salesPrice = items.Sum(i => i.Product.UnitPrice * i.Quantity);
        var discount = items.Sum(i => i.Product.UnitPrice * i.Product.DiscountPercent / 100 * i.Quantity);
        var addDiscount = items.Sum(i => i.Product.UnitPrice * i.Product.AdditionalDiscountPercent / 100 * i.Quantity);
        var gstPercent = items.FirstOrDefault()?.Product.Cgstpercent ?? 0;
        var gstCharge = (salesPrice - discount - addDiscount) * gstPercent / 100;
        var subTotal = salesPrice - discount - addDiscount;
        var grandTotal = subTotal + gstCharge;

        return new CartCalculationDto
        {
            Count = count,
            SalesPrice = salesPrice,
            Discount = discount,
            AddDiscount = addDiscount,
            GST = gstPercent.ToString("F2"),
            GSTCharge = gstCharge,
            SubTotal = subTotal,
            GrandTotal = grandTotal
        };
    }

    private static CartCalculationDto CalculateCart(List<GuestCartItem> items)
    {
        var count = items.Sum(i => (int)i.Quantity);
        var salesPrice = items.Sum(i => i.Product.UnitPrice * i.Quantity);
        var discount = items.Sum(i => i.Product.UnitPrice * i.Product.DiscountPercent / 100 * i.Quantity);
        var addDiscount = items.Sum(i => i.Product.UnitPrice * i.Product.AdditionalDiscountPercent / 100 * i.Quantity);
        var gstPercent = items.FirstOrDefault()?.Product.Cgstpercent ?? 0;
        var gstCharge = (salesPrice - discount - addDiscount) * gstPercent / 100;
        var subTotal = salesPrice - discount - addDiscount;
        var grandTotal = subTotal + gstCharge;

        return new CartCalculationDto
        {
            Count = count,
            SalesPrice = salesPrice,
            Discount = discount,
            AddDiscount = addDiscount,
            GST = gstPercent.ToString("F2"),
            GSTCharge = gstCharge,
            SubTotal = subTotal,
            GrandTotal = grandTotal
        };
    }
}
