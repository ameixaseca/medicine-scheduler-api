using Microsoft.AspNetCore.Mvc;
using MedicineScheduler.Api.DTOs.Auth;
using MedicineScheduler.Api.Services;

namespace MedicineScheduler.Api.Controllers;

/// <summary>Authentication – register, login, token refresh and logout.</summary>
[ApiController]
[Route("auth")]
[Produces("application/json")]
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

    /// <summary>Register a new user account.</summary>
    /// <remarks>Sets an HTTP-only <c>refreshToken</c> cookie valid for 7 days.</remarks>
    /// <response code="201">Account created. Returns the access token.</response>
    /// <response code="400">Validation error.</response>
    /// <response code="409">Email already registered.</response>
    [HttpPost("register")]
    [ProducesResponseType(typeof(AuthResponse), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req)
    {
        var (response, refreshToken) = await authService.RegisterAsync(req);
        Response.Cookies.Append(RefreshCookieName, refreshToken, RefreshCookieOptions);
        return StatusCode(201, response);
    }

    /// <summary>Log in with email and password.</summary>
    /// <remarks>Sets an HTTP-only <c>refreshToken</c> cookie valid for 7 days.</remarks>
    /// <response code="200">Login successful. Returns the access token.</response>
    /// <response code="401">Invalid credentials.</response>
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponse), 200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        var (response, refreshToken) = await authService.LoginAsync(req);
        Response.Cookies.Append(RefreshCookieName, refreshToken, RefreshCookieOptions);
        return Ok(response);
    }

    /// <summary>Refresh the access token using the HTTP-only refresh token cookie.</summary>
    /// <response code="200">Returns a new access token and rotates the refresh cookie.</response>
    /// <response code="401">Refresh token is missing, invalid or expired.</response>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(AuthResponse), 200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> Refresh()
    {
        var token = Request.Cookies[RefreshCookieName];
        if (string.IsNullOrEmpty(token)) return Unauthorized();

        var (response, newRefresh) = await authService.RefreshAsync(token);
        Response.Cookies.Append(RefreshCookieName, newRefresh, RefreshCookieOptions);
        return Ok(response);
    }

    /// <summary>Log out and clear the refresh token cookie.</summary>
    /// <response code="204">Logged out successfully.</response>
    [HttpPost("logout")]
    [ProducesResponseType(204)]
    public IActionResult Logout()
    {
        Response.Cookies.Delete(RefreshCookieName);
        return NoContent();
    }
}
