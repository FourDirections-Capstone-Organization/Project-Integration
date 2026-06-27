using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OperationalSystem.Api.Models.DTOs;
using OperationalSystem.Api.Services;

namespace OperationalSystem.Api.Controllers;

[ApiController]
[Route("api/integration")]
[Authorize(Roles = "Delivery.ExternalService,SystemAdmin")]
public class IntegrationController : ControllerBase
{
    private readonly IProductService _productService;

    public IntegrationController(IProductService productService)
    {
        _productService = productService;
    }

    [HttpGet("products")]
    public async Task<IActionResult> GetAllProducts()
    {
        var products = await _productService.GetAllAsync();
        return Ok(products);
    }

    [HttpGet("products/{id}")]
    public async Task<IActionResult> GetProductById(Guid id)
    {
        var product = await _productService.GetByIdAsync(id);
        if (product == null) return NotFound();
        return Ok(product);
    }
}
