using System.ComponentModel.DataAnnotations;

namespace AuthService.Api.Models.DTOs;

public class LoginRequest
{
    [Required]
    public string EmployeeNumber { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}
