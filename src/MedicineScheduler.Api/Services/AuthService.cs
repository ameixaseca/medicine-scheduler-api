using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using MedicineScheduler.Api.Data;
using MedicineScheduler.Api.DTOs.Auth;
using MedicineScheduler.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace MedicineScheduler.Api.Services;

public class AuthService(AppDbContext db, IConfiguration config, TimeProvider time)
{
    public async Task<(AuthResponse Response, string RefreshToken)> RegisterAsync(RegisterRequest req)
    {
        if (await db.Users.AnyAsync(u => u.Email == req.Email))
            throw new InvalidOperationException("Email already registered.");

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = req.Email,
            Name = req.Name,
            Timezone = req.Timezone,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
            NotificationPreference = NotificationPreference.Push
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        return BuildTokens(user);
    }

    public async Task<(AuthResponse Response, string RefreshToken)> LoginAsync(LoginRequest req)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Email == req.Email && !u.IsDeleted)
            ?? throw new UnauthorizedAccessException("Invalid credentials.");

        if (!BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
            throw new UnauthorizedAccessException("Invalid credentials.");

        return BuildTokens(user);
    }

    public async Task<(AuthResponse Response, string RefreshToken)> RefreshAsync(string refreshToken)
    {
        var principal = ValidateRefreshToken(refreshToken);
        var userId = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var user = await db.Users.FindAsync(userId)
            ?? throw new UnauthorizedAccessException("User not found.");

        return BuildTokens(user);
    }

    private (AuthResponse, string) BuildTokens(User user)
    {
        var accessToken = GenerateJwt(user, TimeSpan.FromHours(1));
        var refreshToken = GenerateJwt(user, TimeSpan.FromDays(7));
        return (new AuthResponse(accessToken), refreshToken);
    }

    private string GenerateJwt(User user, TimeSpan lifetime)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var now = time.GetUtcNow().UtcDateTime;

        var token = new JwtSecurityToken(
            issuer: config["Jwt:Issuer"],
            audience: config["Jwt:Audience"],
            claims:
            [
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            ],
            notBefore: now,
            expires: now.Add(lifetime),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private ClaimsPrincipal ValidateRefreshToken(string token)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!));
        var handler = new JwtSecurityTokenHandler();
        return handler.ValidateToken(token, new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = key,
            ValidIssuer = config["Jwt:Issuer"],
            ValidAudience = config["Jwt:Audience"],
            ClockSkew = TimeSpan.Zero
        }, out _);
    }
}
