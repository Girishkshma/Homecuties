using HC.Business.Dtos;
using HC.Data;
using HC.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace HC.Business;

public class AdminDashboardService : IAdminDashboardService
{
    private readonly HomecutiesDbContext _context;

    public AdminDashboardService(HomecutiesDbContext context)
    {
        _context = context;
    }

    public async Task<DashboardStatsDto> GetDashboardStatsAsync()
    {
        var today = DateTime.UtcNow.Date;
        var monthStart = new DateTime(today.Year, today.Month, 1);

        var stats = new DashboardStatsDto
        {
            TotalProducts = await _context.Products.CountAsync(),
            TotalOrders = await _context.Orders.CountAsync(),
            TotalCustomers = await _context.Customers.CountAsync(),
            TotalPartners = await _context.Partners.CountAsync(),
            TotalVendors = await _context.Vendors.CountAsync(),
            PendingOrders = await _context.Orders.CountAsync(o => o.OrderStatusId == 1), // Assuming 1 = Pending
            TodayRevenue = await _context.Orders
                .Where(o => o.OrderDate >= today)
                .SumAsync(o => (decimal?)o.OrderItems.Sum(oi => oi.UnitPrice)) ?? 0,
            MonthlyRevenue = await _context.Orders
                .Where(o => o.OrderDate >= monthStart)
                .SumAsync(o => (decimal?)o.OrderItems.Sum(oi => oi.UnitPrice)) ?? 0
        };

        return stats;
    }

    public async Task<List<AdminProductListDto>> GetProductsAsync()
    {
        return await _context.Products
            .Include(p => p.ProductStatus)
            .Include(p => p.CreatedByNavigation)
            .OrderByDescending(p => p.CreatedOn)
            .Select(p => new AdminProductListDto
            {
                ProductId = p.ProductId,
                ProductName = p.ProductName,
                ProductTitle = p.ProductTitle,
                UnitPrice = p.UnitPrice,
                Status = p.ProductStatus.ProductStatusName,
                DisplayOnHomePage = p.DisplayOnHomePage,
                CreatedOn = p.CreatedOn,
                CreatedBy = p.CreatedByNavigation.FirstName + " " + p.CreatedByNavigation.LastName
            })
            .ToListAsync();
    }

    public async Task<AdminProductDetailDto?> GetProductDetailAsync(int productId)
    {
        return await _context.Products
            .Include(p => p.ProductCategories)
            .Include(p => p.ProductFeatures)
            .Include(p => p.ProductImages)
            .Where(p => p.ProductId == productId)
            .Select(p => new AdminProductDetailDto
            {
                ProductId = p.ProductId,
                ProductName = p.ProductName,
                ProductTitle = p.ProductTitle,
                ProductDescription = p.ProductDescription,
                DisplayOnHomePage = p.DisplayOnHomePage,
                ProductStatusId = p.ProductStatusId,
                UnitPrice = p.UnitPrice,
                Hsncode = p.Hsncode,
                PackagingCharge = p.PackagingCharge,
                StorageCharge = p.StorageCharge,
                DiscountPercent = p.DiscountPercent,
                AdditionalDiscountPercent = p.AdditionalDiscountPercent,
                DeliveryCharge = p.DeliveryCharge,
                ProfitMarginPercent = p.ProfitMarginPercent,
                Cgstpercent = p.Cgstpercent,
                Sgstpercent = p.Sgstpercent,
                Igstpercent = p.Igstpercent,
                CategoryIds = p.ProductCategories.Select(pc => pc.CategoryId).ToList(),
                Features = p.ProductFeatures.Select(f => new AdminProductFeatureDto
                {
                    ProductFeatureId = f.ProductFeatureId,
                    Feature = f.ProductFeature1,
                    IsActive = f.IsActive
                }).ToList(),
                Images = p.ProductImages.Select(i => new AdminProductImageDto
                {
                    ProductImageId = i.ProductImageId,
                    ImageUrl = i.ImageUrl,
                    ImageTypeId = i.ImageTypeId,
                    ImageIndex = i.ImageIndex,
                    IsPromoImage = i.IsPromoImage,
                    IsActive = i.IsActive
                }).ToList()
            })
            .FirstOrDefaultAsync();
    }

    public async Task<AdminResultDto> CreateProductAsync(CreateProductRequest request, long userId)
    {
        var product = new Product
        {
            ProductName = request.ProductName,
            ProductTitle = request.ProductTitle,
            ProductDescription = request.ProductDescription,
            DisplayOnHomePage = request.DisplayOnHomePage,
            ProductStatusId = request.ProductStatusId,
            UnitPrice = request.UnitPrice,
            Hsncode = request.Hsncode,
            PackagingCharge = request.PackagingCharge,
            StorageCharge = request.StorageCharge,
            DiscountPercent = request.DiscountPercent,
            AdditionalDiscountPercent = request.AdditionalDiscountPercent,
            DeliveryCharge = request.DeliveryCharge,
            ProfitMarginPercent = request.ProfitMarginPercent,
            Cgstpercent = request.Cgstpercent,
            Sgstpercent = request.Sgstpercent,
            Igstpercent = request.Igstpercent,
            CreatedBy = userId,
            CreatedOn = DateTime.UtcNow,
            ModifiedBy = userId,
            ModifiedOn = DateTime.UtcNow
        };

        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        // Add categories
        foreach (var catId in request.CategoryIds)
        {
            _context.ProductCategories.Add(new ProductCategory
            {
                ProductId = product.ProductId,
                CategoryId = catId,
                IsActive = true
            });
        }

        // Add features
        foreach (var feature in request.Features)
        {
            _context.ProductFeatures.Add(new ProductFeature
            {
                ProductId = product.ProductId,
                ProductFeature1 = feature.Feature,
                IsActive = feature.IsActive
            });
        }

        // Add images
        foreach (var image in request.Images)
        {
            _context.ProductImages.Add(new ProductImage
            {
                ProductId = product.ProductId,
                ImageUrl = image.ImageUrl,
                ImageTypeId = image.ImageTypeId,
                ImageIndex = image.ImageIndex,
                IsPromoImage = image.IsPromoImage,
                IsActive = image.IsActive
            });
        }

        await _context.SaveChangesAsync();

        return new AdminResultDto
        {
            Result = 1,
            Messages = new[] { "Product created successfully." }
        };
    }

    public async Task<AdminResultDto> UpdateProductAsync(int productId, CreateProductRequest request, long userId)
    {
        var product = await _context.Products
            .Include(p => p.ProductCategories)
            .Include(p => p.ProductFeatures)
            .Include(p => p.ProductImages)
            .FirstOrDefaultAsync(p => p.ProductId == productId);

        if (product == null)
        {
            return new AdminResultDto
            {
                Result = 0,
                Messages = new[] { "Product not found." }
            };
        }

        // Update product fields
        product.ProductName = request.ProductName;
        product.ProductTitle = request.ProductTitle;
        product.ProductDescription = request.ProductDescription;
        product.DisplayOnHomePage = request.DisplayOnHomePage;
        product.ProductStatusId = request.ProductStatusId;
        product.UnitPrice = request.UnitPrice;
        product.Hsncode = request.Hsncode;
        product.PackagingCharge = request.PackagingCharge;
        product.StorageCharge = request.StorageCharge;
        product.DiscountPercent = request.DiscountPercent;
        product.AdditionalDiscountPercent = request.AdditionalDiscountPercent;
        product.DeliveryCharge = request.DeliveryCharge;
        product.ProfitMarginPercent = request.ProfitMarginPercent;
        product.Cgstpercent = request.Cgstpercent;
        product.Sgstpercent = request.Sgstpercent;
        product.Igstpercent = request.Igstpercent;
        product.ModifiedBy = userId;
        product.ModifiedOn = DateTime.UtcNow;

        // Remove existing categories
        _context.ProductCategories.RemoveRange(product.ProductCategories);

        // Add new categories
        foreach (var catId in request.CategoryIds)
        {
            _context.ProductCategories.Add(new ProductCategory
            {
                ProductId = product.ProductId,
                CategoryId = catId,
                IsActive = true
            });
        }

        // Remove existing features
        _context.ProductFeatures.RemoveRange(product.ProductFeatures);

        // Add new features
        foreach (var feature in request.Features)
        {
            _context.ProductFeatures.Add(new ProductFeature
            {
                ProductId = product.ProductId,
                ProductFeature1 = feature.Feature,
                IsActive = feature.IsActive
            });
        }

        // Remove existing images
        _context.ProductImages.RemoveRange(product.ProductImages);

        // Add new images
        foreach (var image in request.Images)
        {
            _context.ProductImages.Add(new ProductImage
            {
                ProductId = product.ProductId,
                ImageUrl = image.ImageUrl,
                ImageTypeId = image.ImageTypeId,
                ImageIndex = image.ImageIndex,
                IsPromoImage = image.IsPromoImage,
                IsActive = image.IsActive
            });
        }

        await _context.SaveChangesAsync();

        return new AdminResultDto
        {
            Result = 1,
            Messages = new[] { "Product updated successfully." }
        };
    }

    public async Task<AdminResultDto> DeleteProductAsync(int productId)
    {
        var product = await _context.Products
            .Include(p => p.ProductCategories)
            .Include(p => p.ProductFeatures)
            .Include(p => p.ProductImages)
            .FirstOrDefaultAsync(p => p.ProductId == productId);

        if (product == null)
        {
            return new AdminResultDto
            {
                Result = 0,
                Messages = new[] { "Product not found." }
            };
        }

        _context.ProductCategories.RemoveRange(product.ProductCategories);
        _context.ProductFeatures.RemoveRange(product.ProductFeatures);
        _context.ProductImages.RemoveRange(product.ProductImages);
        _context.Products.Remove(product);

        await _context.SaveChangesAsync();

        return new AdminResultDto
        {
            Result = 1,
            Messages = new[] { "Product deleted successfully." }
        };
    }

    public async Task<List<AdminOrderListDto>> GetOrdersAsync()
    {
        return await _context.Orders
            .Include(o => o.Customer)
            .Include(o => o.OrderStatus)
            .Include(o => o.OrderItems)
            .OrderByDescending(o => o.OrderDate)
            .Select(o => new AdminOrderListDto
            {
                OrderId = o.OrderId,
                OrderNumber = "ORD-" + o.OrderId.ToString("D6"),
                OrderDate = o.OrderDate,
                CustomerName = o.Customer.FirstName + " " + (o.Customer.LastName ?? ""),
                Status = o.OrderStatus.Status,
                TotalAmount = o.OrderItems.Sum(oi => oi.UnitPrice),
                ItemCount = o.OrderItems.Count
            })
            .ToListAsync();
    }

    public async Task<AdminOrderDetailDto?> GetOrderDetailAsync(long orderId)
    {
        return await _context.Orders
            .Include(o => o.Customer)
            .Include(o => o.OrderStatus)
            .Include(o => o.Seller)
            .Include(o => o.BillingAddress)
            .Include(o => o.ShippingAddress)
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.SkuNavigation)
            .Include(o => o.OrderHistories)
                .ThenInclude(oh => oh.Order)
            .Where(o => o.OrderId == orderId)
            .Select(o => new AdminOrderDetailDto
            {
                OrderId = o.OrderId,
                OrderNumber = "ORD-" + o.OrderId.ToString("D6"),
                OrderDate = o.OrderDate,
                CustomerName = o.Customer.FirstName + " " + (o.Customer.LastName ?? ""),
                CustomerEmail = o.Customer.EmailId,
                Status = o.OrderStatus.Status,
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
                History = o.OrderHistories.Select(oh => new AdminOrderHistoryDto
                {
                    HistoryDate = oh.HistoryDate,
                    Status = oh.Order.OrderStatus.Status,
                    Comments = oh.Comments
                }).ToList()
            })
            .FirstOrDefaultAsync();
    }

    public async Task<List<AdminCustomerListDto>> GetCustomersAsync()
    {
        return await _context.Customers
            .Include(c => c.CustomerStatus)
            .OrderByDescending(c => c.CreatedOn)
            .Select(c => new AdminCustomerListDto
            {
                CustomerId = c.CustomerId,
                FirstName = c.FirstName,
                MiddleName = c.MiddleName,
                LastName = c.LastName,
                EmailId = c.EmailId,
                MobileNumber = c.MobileNumber,
                MobileVerified = c.MobileVerified,
                EmailVerified = c.EmailVerfied,
                CreatedOn = c.CreatedOn,
                ModifiedOn = c.ModifiedOn,
                CustomerStatusId = c.CustomerStatusId,
                Status = c.CustomerStatus.CustomerStatus1
            })
            .ToListAsync();
    }

    public async Task<AdminCustomerDetailDto?> GetCustomerDetailAsync(long customerId)
    {
        return await _context.Customers
            .Include(c => c.CustomerStatus)
            .Include(c => c.CustomerAddresses)
            .Include(c => c.Orders)
            .Where(c => c.CustomerId == customerId)
            .Select(c => new AdminCustomerDetailDto
            {
                CustomerId = c.CustomerId,
                FirstName = c.FirstName,
                MiddleName = c.MiddleName,
                LastName = c.LastName,
                EmailId = c.EmailId,
                MobileNumber = c.MobileNumber,
                MobileVerified = c.MobileVerified,
                EmailVerified = c.EmailVerfied,
                CreatedOn = c.CreatedOn,
                ModifiedOn = c.ModifiedOn,
                CustomerStatusId = c.CustomerStatusId,
                Status = c.CustomerStatus.CustomerStatus1,
                Addresses = c.CustomerAddresses.Select(a => new AdminCustomerAddressDto
                {
                    AddressId = a.AddressId,
                    AddressTitle = a.AddressTitle,
                    ContactName = a.ContactName,
                    AddressLine1 = a.AddressLine1,
                    AddressLine2 = a.AddressLine2,
                    City = a.City,
                    State = a.State,
                    Country = a.Country,
                    Zipcode = a.Zipcode,
                    MobileNumber = a.MobileNumber
                }).ToList(),
                OrderCount = c.Orders.Count,
                TotalSpent = c.Orders.SelectMany(o => o.OrderItems).Sum(oi => (decimal?)oi.UnitPrice) ?? 0
            })
            .FirstOrDefaultAsync();
    }

    public async Task<AdminResultDto> UpdateCustomerStatusAsync(long customerId, short customerStatusId)
    {
        var customer = await _context.Customers.FindAsync(customerId);
        if (customer == null)
        {
            return new AdminResultDto
            {
                Result = 0,
                Messages = new[] { "Customer not found." }
            };
        }

        var statusExists = await _context.CustomerStatuses.AnyAsync(cs => cs.CustomerStatusId == customerStatusId);
        if (!statusExists)
        {
            return new AdminResultDto
            {
                Result = 0,
                Messages = new[] { "Invalid customer status." }
            };
        }

        customer.CustomerStatusId = customerStatusId;
        customer.ModifiedOn = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return new AdminResultDto
        {
            Result = 1,
            Messages = new[] { "Customer status updated successfully." }
        };
    }

    public async Task<List<AdminCustomerListDto>> SearchCustomersAsync(string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return await GetCustomersAsync();

        var term = searchTerm.Trim().ToLower();
        return await _context.Customers
            .Include(c => c.CustomerStatus)
            .Where(c => c.FirstName.ToLower().Contains(term) ||
                        (c.LastName != null && c.LastName.ToLower().Contains(term)) ||
                        c.EmailId.ToLower().Contains(term) ||
                        (c.MobileNumber != null && c.MobileNumber.Contains(term)))
            .OrderByDescending(c => c.CreatedOn)
            .Select(c => new AdminCustomerListDto
            {
                CustomerId = c.CustomerId,
                FirstName = c.FirstName,
                MiddleName = c.MiddleName,
                LastName = c.LastName,
                EmailId = c.EmailId,
                MobileNumber = c.MobileNumber,
                MobileVerified = c.MobileVerified,
                EmailVerified = c.EmailVerfied,
                CreatedOn = c.CreatedOn,
                ModifiedOn = c.ModifiedOn,
                CustomerStatusId = c.CustomerStatusId,
                Status = c.CustomerStatus.CustomerStatus1
            })
            .ToListAsync();
    }


    public async Task<List<AdminPartnerListDto>> GetPartnersAsync()
    {
        return await _context.Partners
            .Include(p => p.PartnerStatus)
            .OrderByDescending(p => p.LastModifiedOn)
            .Select(p => new AdminPartnerListDto
            {
                PartnerId = p.PartnerId,
                PartnerName = p.PartnerName,
                Status = p.PartnerStatus.PartnerStatus1,
                LastModifiedOn = p.LastModifiedOn
            })
            .ToListAsync();
    }

    public async Task<AdminPartnerDetailDto?> GetPartnerDetailAsync(int partnerId)
    {
        return await _context.Partners
            .Include(p => p.PartnerStatus)
            .Include(p => p.PartnersUsers)
                .ThenInclude(pu => pu.User)
                    .ThenInclude(u => u.UserRoles)
                        .ThenInclude(ur => ur.Role)
            .Include(p => p.Inventories)
            .Where(p => p.PartnerId == partnerId)
            .Select(p => new AdminPartnerDetailDto
            {
                PartnerId = p.PartnerId,
                PartnerName = p.PartnerName,
                Status = p.PartnerStatus.PartnerStatus1,
                LastModifiedOn = p.LastModifiedOn,
                Users = p.PartnersUsers.Where(pu => pu.IsActive).Select(pu => new AdminPartnerUserDto
                {
                    UserId = pu.User.UserId,
                    UserName = pu.User.FirstName + " " + (pu.User.LastName ?? ""),
                    LoginId = pu.User.LoginId,
                    EmailId = pu.User.EmailId,
                    MobileNumber = pu.User.MobileNumber,
                    IsActive = pu.User.IsActive ?? false,
                    Roles = pu.User.UserRoles.Where(ur => ur.IsActive).Select(ur => ur.Role.RoleName).ToList()
                }).ToList(),
                InventoryCount = p.Inventories.Count,
                OrderCount = _context.Orders.Count(o => o.SellerId == p.PartnerId)
            })
            .FirstOrDefaultAsync();
    }

    public async Task<List<AdminVendorListDto>> GetVendorsAsync()
    {
        return await _context.Vendors
            .OrderBy(v => v.VendorName)
            .Select(v => new AdminVendorListDto
            {
                VendorId = v.VendorId,
                VendorName = v.VendorName,
                VendorAddress = v.VendorAddress,
                Mobile = v.Mobile,
                IsActive = v.IsActive
            })
            .ToListAsync();
    }

    public async Task<AdminVendorDetailDto?> GetVendorDetailAsync(short vendorId)
    {
        return await _context.Vendors
            .Include(v => v.VendorsUsers)
                .ThenInclude(vu => vu.User)
                    .ThenInclude(u => u.UserRoles)
                        .ThenInclude(ur => ur.Role)
            .Include(v => v.Purchases)
            .Where(v => v.VendorId == vendorId)
            .Select(v => new AdminVendorDetailDto
            {
                VendorId = v.VendorId,
                VendorName = v.VendorName,
                VendorAddress = v.VendorAddress,
                Mobile = v.Mobile,
                Remarks = v.Remarks,
                IsActive = v.IsActive,
                Users = v.VendorsUsers.Where(vu => vu.IsActive).Select(vu => new AdminVendorUserDto
                {
                    UserId = vu.User.UserId,
                    UserName = vu.User.FirstName + " " + (vu.User.LastName ?? ""),
                    LoginId = vu.User.LoginId,
                    EmailId = vu.User.EmailId,
                    MobileNumber = vu.User.MobileNumber,
                    IsActive = vu.User.IsActive ?? false,
                    Roles = vu.User.UserRoles.Where(ur => ur.IsActive).Select(ur => ur.Role.RoleName).ToList()
                }).ToList(),
                PurchaseCount = v.Purchases.Count
            })
            .FirstOrDefaultAsync();
    }

    public async Task<List<AdminUserListDto>> GetAdminUsersAsync()
    {
        return await _context.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .OrderBy(u => u.FirstName)
            .Select(u => new AdminUserListDto
            {
                UserId = u.UserId,
                LoginId = u.LoginId,
                FirstName = u.FirstName,
                LastName = u.LastName,
                EmailId = u.EmailId,
                IsActive = u.IsActive ?? false,
                Roles = u.UserRoles.Where(ur => ur.IsActive).Select(ur => ur.Role.RoleName).ToList()
            })
            .ToListAsync();
    }

    public async Task<List<AdminCategoryDto>> GetCategoriesAsync()
    {
        return await _context.Categories
            .Select(c => new AdminCategoryDto
            {
                CategoryId = c.CategoryId,
                CategoryName = c.CategoryName,
                ParentCategoryId = c.ParentCategoryId
            })
            .ToListAsync();
    }

    public async Task<List<AdminCategoryDto>> GetCategoryTreeAsync()
    {
        var categories = await _context.Categories
            .Select(c => new AdminCategoryDto
            {
                CategoryId = c.CategoryId,
                CategoryName = c.CategoryName,
                ParentCategoryId = c.ParentCategoryId,
                ParentCategoryName = c.ParentCategory != null ? c.ParentCategory.CategoryName : null
            })
            .ToListAsync();

        return categories;
    }
}
