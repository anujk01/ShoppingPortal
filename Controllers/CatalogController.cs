using Microsoft.AspNetCore.Mvc;
using ShoppingPortal.Application;

namespace ShoppingPortal.Controllers;

[ApiController]
[Route("api/catalog")]
public sealed class CatalogController(ICatalogService catalog) : ControllerBase
{
    [HttpGet("categories")]
    public IActionResult Categories() => Ok(catalog.GetCategories());

    [HttpGet("products")]
    public IActionResult Products([FromQuery] string? categoryId = null) => Ok(catalog.GetProducts(categoryId));
}
