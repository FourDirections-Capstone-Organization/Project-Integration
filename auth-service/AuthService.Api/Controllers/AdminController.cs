using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AuthService.Api.Models.DTOs;
using AuthService.Api.Services;

namespace AuthService.Api.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "SystemAdmin")]
public class AdminController : ControllerBase
{
    private readonly IAuthService _authService;

    public AdminController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("accounts")]
    public async Task<IActionResult> CreateAccount([FromBody] CreateAccountRequest request)
    {
        var success = await _authService.CreateAccountAsync(request);
        if (!success)
            return Conflict(new { message = "Employee number already exists" });

        return Ok(new { message = "Account created successfully" });
    }
}
