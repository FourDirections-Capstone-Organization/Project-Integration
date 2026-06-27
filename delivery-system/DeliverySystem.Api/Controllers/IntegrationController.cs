using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DeliverySystem.Api.Models.DTOs;
using DeliverySystem.Api.Services;

namespace DeliverySystem.Api.Controllers;

[ApiController]
[Route("api/integration")]
[Authorize(Roles = "Operational.ExternalService,SystemAdmin")]
public class IntegrationController : ControllerBase
{
    private readonly IOrderService _orderService;

    public IntegrationController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    [HttpGet("orders")]
    public async Task<IActionResult> GetAllOrders()
    {
        var orders = await _orderService.GetAllAsync();
        return Ok(orders);
    }

    [HttpGet("orders/{id}")]
    public async Task<IActionResult> GetOrderById(Guid id)
    {
        var order = await _orderService.GetByIdAsync(id);
        if (order == null) return NotFound();
        return Ok(order);
    }
}
