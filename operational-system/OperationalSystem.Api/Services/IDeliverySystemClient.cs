using OperationalSystem.Api.Models.DTOs;

namespace OperationalSystem.Api.Services;

public interface IDeliverySystemClient
{
    Task<DeliveryOrderResponse?> GetOrderAsync(Guid orderId);
    Task<List<DeliveryOrderResponse>> GetAllOrdersAsync();
}
