using HC.Business.Dtos;

namespace HC.Business;

public interface IProductService
{
    Task<IEnumerable<ProductDto>> GetProductsForHomepageAsync();
    Task<IEnumerable<ProductDto>> GetActiveProductsAsync();
    Task<IEnumerable<ProductDto>> SearchProductsAsync(string? query);
    Task<SearchSuggestionsDto> GetSearchSuggestionsAsync(string? query);
    Task<HomeStatsDto> GetHomeStatsAsync();
    Task<ProductDto?> GetProductAsync(int productId);
    Task<IEnumerable<ProductDto>> GetProductsByCategoryAsync(short categoryId);
    Task<IEnumerable<CategoryDto>> GetCategoriesAsync();
    Task<CategoryDto?> GetCategoryAsync(short categoryId);
}
