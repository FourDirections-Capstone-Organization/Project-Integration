using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using AuthService.Api.Models.Entities;

namespace AuthService.Api.Services;

public class JwtService : IJwtService
{
    private readonly IConfiguration _configuration;

    public JwtService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GenerateToken(UserAccount user)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim("employeeNumber", user.EmployeeNumber),
            new Claim(ClaimTypes.Name, user.Name),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Iss, _configuration["Jwt:Issuer"]!),
            new Claim(JwtRegisteredClaimNames.Aud, _configuration["Jwt:Audience"]!)
        };

        var token = new JwtSecurityToken(
            expires: DateTime.UtcNow.AddMinutes(
                double.Parse(_configuration["Jwt:AccessTokenExpirationMinutes"] ?? "60")),
            signingCredentials: credentials,
            claims: claims
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
