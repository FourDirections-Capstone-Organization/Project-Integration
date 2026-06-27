namespace AuthService.Api.Models.DTOs;

public class LoginResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string EmployeeNumber { get; set; } = string.Empty;
}
