using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DeliverySystem.Api.Models.DTOs;
using DeliverySystem.Api.Services;

namespace DeliverySystem.Api.Controllers;

[ApiController]
[Route("api/orders")]
[Authorize]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;

    public OrdersController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var orders = await _orderService.GetAllAsync();
        return Ok(orders);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var order = await _orderService.GetByIdAsync(id);
        if (order == null) return NotFound();
        return Ok(order);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateOrderDto dto)
    {
        var order = await _orderService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = order.Id }, order);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateOrderDto dto)
    {
        var order = await _orderService.UpdateAsync(id, dto);
        if (order == null) return NotFound();
        return Ok(order);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var deleted = await _orderService.DeleteAsync(id);
        if (!deleted) return NotFound();
        return NoContent();
    }
}
