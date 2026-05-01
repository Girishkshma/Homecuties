using HC.Business.Dtos;

namespace HC.Business;

public interface IProductService
{
    Task<IEnumerable<ProductDto>> GetProductsForHomepageAsync();
    Task<ProductDto?> GetProductAsync(int productId);
    Task<IEnumerable<ProductDto>> GetProductsByCategoryAsync(short categoryId);
    Task<IEnumerable<CategoryDto>> GetCategoriesAsync();
    Task<CategoryDto?> GetCategoryAsync(short categoryId);
}
