// ============================================================
// AdminDashboardService.Products.cs
// Partial class: AdminDashboardService - Products operations
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

    public async Task<AdminResultDto> DeactivateProductAsync(int productId, long userId)
    {
        // Products are never hard-deleted - they are disabled by setting the
        // "Suspended" (2) status so they stop appearing on the storefront.
        var product = await _context.Products.FirstOrDefaultAsync(p => p.ProductId == productId);
        if (product == null)
        {
            return new AdminResultDto
            {
                Result = 0,
                Messages = new[] { "Product not found." }
            };
        }

        var suspendedStatusId = await _context.ProductStatuses
            .Where(s => s.ProductStatusName == "Suspended")
            .Select(s => s.ProductStatusId)
            .FirstOrDefaultAsync();
        if (suspendedStatusId == 0)
            suspendedStatusId = 2; // fallback: Suspended

        product.ProductStatusId = suspendedStatusId;
        product.ModifiedBy = userId;
        product.ModifiedOn = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return new AdminResultDto
        {
            Result = 1,
            Messages = new[] { "Product disabled successfully." }
        };
    }

    public async Task<ProductFormOptionsDto> GetProductFormOptionsAsync()
    {
        return new ProductFormOptionsDto
        {
            Statuses = await _context.ProductStatuses
                .OrderBy(s => s.ProductStatusId)
                .Select(s => new ProductStatusOptionDto
                {
                    ProductStatusId = s.ProductStatusId,
                    ProductStatusName = s.ProductStatusName
                })
                .ToListAsync(),
            ImageTypes = await _context.ImageTypes
                .OrderBy(t => t.ImageTypeId)
                .Select(t => new ImageTypeOptionDto
                {
                    ImageTypeId = t.ImageTypeId,
                    ImageTypeName = t.ImageTypeName,
                    ShortCode = t.ShortCode
                })
                .ToListAsync()
        };
    }

}
