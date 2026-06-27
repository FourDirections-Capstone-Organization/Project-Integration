using Microsoft.EntityFrameworkCore;
using AuthService.Api.Data;
using AuthService.Api.Models.DTOs;
using AuthService.Api.Models.Entities;

namespace AuthService.Api.Services;

public class AuthService : IAuthService
{
    private readonly AppDbContext _context;
    private readonly IJwtService _jwtService;

    public AuthService(AppDbContext context, IJwtService jwtService)
    {
        _context = context;
        _jwtService = jwtService;
    }

    public async Task<LoginResponse?> LoginAsync(LoginRequest request)
    {
        var user = await _context.UserAccounts
            .FirstOrDefaultAsync(u => u.EmployeeNumber == request.EmployeeNumber && u.IsActive);

        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return null;

        return new LoginResponse
        {
            AccessToken = _jwtService.GenerateToken(user),
            Role = user.Role,
            Name = user.Name,
            EmployeeNumber = user.EmployeeNumber
        };
    }

    public async Task<bool> RegisterAsync(RegisterRequest request)
    {
        if (await _context.UserAccounts.AnyAsync(u => u.EmployeeNumber == request.EmployeeNumber))
            return false;

        var user = new UserAccount
        {
            EmployeeNumber = request.EmployeeNumber,
            Name = request.Name,
            Email = request.Email,
            Role = request.Role,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password)
        };

        _context.UserAccounts.Add(user);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> CreateAccountAsync(CreateAccountRequest request)
    {
        if (await _context.UserAccounts.AnyAsync(u => u.EmployeeNumber == request.EmployeeNumber))
            return false;

        var user = new UserAccount
        {
            EmployeeNumber = request.EmployeeNumber,
            Name = request.Name,
            Email = request.Email,
            Role = request.Role,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password)
        };

        _context.UserAccounts.Add(user);
        await _context.SaveChangesAsync();
        return true;
    }
}
