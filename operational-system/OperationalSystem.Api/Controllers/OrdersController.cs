using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OperationalSystem.Api.Services;

namespace OperationalSystem.Api.Controllers;

[ApiController]
[Route("api/orders")]
[Authorize]
public class OrdersController : ControllerBase
{
    private readonly IDeliverySystemClient _deliveryClient;

    public OrdersController(IDeliverySystemClient deliveryClient)
    {
        _deliveryClient = deliveryClient;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var orders = await _deliveryClient.GetAllOrdersAsync();
        return Ok(orders);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var order = await _deliveryClient.GetOrderAsync(id);
        if (order == null) return NotFound();
        return Ok(order);
    }
}
