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
