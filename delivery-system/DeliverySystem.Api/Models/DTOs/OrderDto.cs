using System.ComponentModel.DataAnnotations;

namespace DeliverySystem.Api.Models.DTOs;

public class OrderDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }

    [MaxLength(100)]
    public string ProductName { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int Quantity { get; set; }

    [MaxLength(20)]
    public string Status { get; set; } = "Pending";

    [MaxLength(100)]
    public string CustomerName { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CreateOrderDto
{
    [Required]
    public Guid ProductId { get; set; }

    [Required, MaxLength(100)]
    public string ProductName { get; set; } = string.Empty;

    [Required]
    [Range(1, int.MaxValue)]
    public int Quantity { get; set; }

    [Required, MaxLength(20)]
    public string Status { get; set; } = "Pending";

    [Required, MaxLength(100)]
    public string CustomerName { get; set; } = string.Empty;
}

public class UpdateOrderDto
{
    [Required, MaxLength(100)]
    public string ProductName { get; set; } = string.Empty;

    [Required]
    [Range(1, int.MaxValue)]
    public int Quantity { get; set; }

    [Required, MaxLength(20)]
    public string Status { get; set; } = "Pending";

    [Required, MaxLength(100)]
    public string CustomerName { get; set; } = string.Empty;
}
