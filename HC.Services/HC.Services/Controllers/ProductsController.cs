using HC.Business;
using Microsoft.AspNetCore.Mvc;

namespace HC.Services.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly IProductService _productService;

    public ProductsController(IProductService productService)
    {
        _productService = productService;
    }

    [HttpGet("GetProductsForHomepage")]
    public async Task<ActionResult<IEnumerable<object>>> GetProductsForHomepage()
    {
        var products = await _productService.GetProductsForHomepageAsync();
        return Ok(products);
    }

    [HttpGet("GetActiveProducts")]
    public async Task<ActionResult<IEnumerable<object>>> GetActiveProducts()
    {
        var products = await _productService.GetActiveProductsAsync();
        return Ok(products);
    }

    /// <summary>
    /// The shop searched from the storefront header's search box: the products matching the shopper's words (see
    /// ProductService.SearchProductsAsync). A blank query answers with nothing, so a stray empty search cannot be read
    /// as a request for the whole catalogue.
    /// </summary>
    [HttpGet("SearchProducts")]
    public async Task<ActionResult<IEnumerable<object>>> SearchProducts([FromQuery] string? query)
    {
        var products = await _productService.SearchProductsAsync(query);
        return Ok(products);
    }

    /// <summary>
    /// What the storefront header's search box offers while a shopper is still typing: the collections the word names
    /// and the pieces whose names it appears in (see ProductService.GetSearchSuggestionsAsync). It is asked for while
    /// the words are arriving rather than once they are in, so its answer is a handful of names - no prices, no photos.
    /// </summary>
    [HttpGet("SearchSuggestions")]
    public async Task<ActionResult<object>> SearchSuggestions([FromQuery] string? query)
    {
        var suggestions = await _productService.GetSearchSuggestionsAsync(query);
        return Ok(suggestions);
    }

    /// <summary>Live counts (products / customers / categories) for the storefront hero section.</summary>
    [HttpGet("GetHomeStats")]
    public async Task<ActionResult<object>> GetHomeStats()
    {
        var stats = await _productService.GetHomeStatsAsync();
        return Ok(stats);
    }

    [HttpGet("GetProduct/{productId}")]
    public async Task<ActionResult<object>> GetProduct(string productId)
    {
        if (!int.TryParse(productId, out var id))
            return BadRequest(new { Result = 0, Messages = new[] { "Invalid product ID" } });

        var product = await _productService.GetProductAsync(id);

        if (product == null)
            return NotFound(new { Result = 0, Messages = new[] { "Product not found" } });

        return Ok(product);
    }

    [HttpGet("GetProductsByCategory/{categoryId}")]
    public async Task<ActionResult<IEnumerable<object>>> GetProductsByCategory(short categoryId)
    {
        var products = await _productService.GetProductsByCategoryAsync(categoryId);
        return Ok(products);
    }

    [HttpGet("GetCategories")]
    public async Task<ActionResult<IEnumerable<object>>> GetCategories()
    {
        var categories = await _productService.GetCategoriesAsync();
        return Ok(categories);
    }

    [HttpGet("GetCategory/{categoryId}")]
    public async Task<ActionResult<object>> GetCategory(short categoryId)
    {
        var category = await _productService.GetCategoryAsync(categoryId);

        if (category == null)
            return NotFound(new { Result = 0, Messages = new[] { "Category not found" } });

        return Ok(category);
    }
}
