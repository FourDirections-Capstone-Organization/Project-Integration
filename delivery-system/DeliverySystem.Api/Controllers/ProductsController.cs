using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DeliverySystem.Api.Services;

namespace DeliverySystem.Api.Controllers;

[ApiController]
[Route("api/products")]
[Authorize]
public class ProductsController : ControllerBase
{
    private readonly IOperationalSystemClient _operationalClient;

    public ProductsController(IOperationalSystemClient operationalClient)
    {
        _operationalClient = operationalClient;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var products = await _operationalClient.GetAllProductsAsync();
        return Ok(products);
    }
}
