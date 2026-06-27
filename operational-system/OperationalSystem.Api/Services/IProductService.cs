using OperationalSystem.Api.Models.DTOs;

namespace OperationalSystem.Api.Services;

public interface IProductService
{
    Task<List<ProductDto>> GetAllAsync();
    Task<ProductDto?> GetByIdAsync(Guid id);
    Task<ProductDto> CreateAsync(CreateProductDto dto);
    Task<ProductDto?> UpdateAsync(Guid id, UpdateProductDto dto);
    Task<bool> DeleteAsync(Guid id);
}
