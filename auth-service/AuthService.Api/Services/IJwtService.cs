using AuthService.Api.Models.Entities;

namespace AuthService.Api.Services;

public interface IJwtService
{
    string GenerateToken(UserAccount user);
}
