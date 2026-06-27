using Microsoft.EntityFrameworkCore;
using DeliverySystem.Api.Data;
using DeliverySystem.Api.Models.DTOs;
using DeliverySystem.Api.Models.Entities;

namespace DeliverySystem.Api.Services;

public class OrderService : IOrderService
{
    private readonly AppDbContext _context;

    public OrderService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<OrderDto>> GetAllAsync()
    {
        return await _context.Orders
            .Select(o => new OrderDto
            {
                Id = o.Id,
                ProductId = o.ProductId,
                ProductName = o.ProductName,
                Quantity = o.Quantity,
                Status = o.Status,
                CustomerName = o.CustomerName,
                CreatedAt = o.CreatedAt,
                UpdatedAt = o.UpdatedAt
            })
            .ToListAsync();
    }

    public async Task<OrderDto?> GetByIdAsync(Guid id)
    {
        var order = await _context.Orders.FindAsync(id);
        if (order == null) return null;

        return new OrderDto
        {
            Id = order.Id,
            ProductId = order.ProductId,
            ProductName = order.ProductName,
            Quantity = order.Quantity,
            Status = order.Status,
            CustomerName = order.CustomerName,
            CreatedAt = order.CreatedAt,
            UpdatedAt = order.UpdatedAt
        };
    }

    public async Task<OrderDto> CreateAsync(CreateOrderDto dto)
    {
        var order = new Order
        {
            ProductId = dto.ProductId,
            ProductName = dto.ProductName,
            Quantity = dto.Quantity,
            Status = dto.Status,
            CustomerName = dto.CustomerName
        };

        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        return new OrderDto
        {
            Id = order.Id,
            ProductId = order.ProductId,
            ProductName = order.ProductName,
            Quantity = order.Quantity,
            Status = order.Status,
            CustomerName = order.CustomerName,
            CreatedAt = order.CreatedAt,
            UpdatedAt = order.UpdatedAt
        };
    }

    public async Task<OrderDto?> UpdateAsync(Guid id, UpdateOrderDto dto)
    {
        var order = await _context.Orders.FindAsync(id);
        if (order == null) return null;

        order.ProductName = dto.ProductName;
        order.Quantity = dto.Quantity;
        order.Status = dto.Status;
        order.CustomerName = dto.CustomerName;
        order.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return new OrderDto
        {
            Id = order.Id,
            ProductId = order.ProductId,
            ProductName = order.ProductName,
            Quantity = order.Quantity,
            Status = order.Status,
            CustomerName = order.CustomerName,
            CreatedAt = order.CreatedAt,
            UpdatedAt = order.UpdatedAt
        };
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var order = await _context.Orders.FindAsync(id);
        if (order == null) return false;

        _context.Orders.Remove(order);
        await _context.SaveChangesAsync();
        return true;
    }
}
