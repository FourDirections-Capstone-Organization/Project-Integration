using System.ComponentModel.DataAnnotations;

namespace AuthService.Api.Models.DTOs;

public class RegisterRequest
{
    [Required, MaxLength(20)]
    public string EmployeeNumber { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(200)]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string Role { get; set; } = string.Empty;
}
