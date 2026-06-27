using System.ComponentModel.DataAnnotations;

namespace DeliverySystem.Api.Models.Entities;

public class Order
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

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

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
