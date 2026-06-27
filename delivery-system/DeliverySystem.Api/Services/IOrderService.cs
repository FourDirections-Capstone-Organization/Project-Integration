using DeliverySystem.Api.Models.DTOs;

namespace DeliverySystem.Api.Services;

public interface IOrderService
{
    Task<List<OrderDto>> GetAllAsync();
    Task<OrderDto?> GetByIdAsync(Guid id);
    Task<OrderDto> CreateAsync(CreateOrderDto dto);
    Task<OrderDto?> UpdateAsync(Guid id, UpdateOrderDto dto);
    Task<bool> DeleteAsync(Guid id);
}
