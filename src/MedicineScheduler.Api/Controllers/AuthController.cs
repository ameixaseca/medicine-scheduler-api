using Microsoft.AspNetCore.Mvc;
using MedicineScheduler.Api.DTOs.Auth;
using MedicineScheduler.Api.Services;

namespace MedicineScheduler.Api.Controllers;

[ApiController]
[Route("auth")]
public class AuthController(AuthService authService, IWebHostEnvironment env) : ControllerBase
{
    private const string RefreshCookieName = "refreshToken";

    // Production (cross-domain Vercel → Fly.io): SameSite=None requires Secure=true
    // Development (same-origin via Vite proxy): SameSite=Lax, Secure not required
    private CookieOptions RefreshCookieOptions => new()
    {
        HttpOnly = true,
        Secure = env.IsProduction(),
        SameSite = env.IsProduction() ? SameSiteMode.None : SameSiteMode.Lax,
        Expires = DateTimeOffset.UtcNow.AddDays(7)
    };

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req)
    {
        var (response, refreshToken) = await authService.RegisterAsync(req);
        Response.Cookies.Append(RefreshCookieName, refreshToken, RefreshCookieOptions);
        return StatusCode(201, response);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        var (response, refreshToken) = await authService.LoginAsync(req);
        Response.Cookies.Append(RefreshCookieName, refreshToken, RefreshCookieOptions);
        return Ok(response);
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh()
    {
        var token = Request.Cookies[RefreshCookieName];
        if (string.IsNullOrEmpty(token)) return Unauthorized();

        var (response, newRefresh) = await authService.RefreshAsync(token);
        Response.Cookies.Append(RefreshCookieName, newRefresh, RefreshCookieOptions);
        return Ok(response);
    }

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        Response.Cookies.Delete(RefreshCookieName);
        return NoContent();
    }
}
