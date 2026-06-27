using AuthService.Api.Models.DTOs;

namespace AuthService.Api.Services;

public interface IAuthService
{
    Task<LoginResponse?> LoginAsync(LoginRequest request);
    Task<bool> RegisterAsync(RegisterRequest request);
    Task<bool> CreateAccountAsync(CreateAccountRequest request);
}
