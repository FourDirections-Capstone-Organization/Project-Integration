using DeliverySystem.Api.Models.DTOs;

namespace DeliverySystem.Api.Services;

public interface IOperationalSystemClient
{
    Task<OperationalProductResponse?> GetProductAsync(Guid productId);
    Task<List<OperationalProductResponse>> GetAllProductsAsync();
}
