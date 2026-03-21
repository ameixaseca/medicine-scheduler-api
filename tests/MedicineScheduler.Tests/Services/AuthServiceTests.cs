using Microsoft.EntityFrameworkCore;
using MedicineScheduler.Api.Data;
using MedicineScheduler.Api.DTOs.Auth;
using MedicineScheduler.Api.Services;
using Microsoft.Extensions.Configuration;

namespace MedicineScheduler.Tests.Services;

public class AuthServiceTests
{
    private AppDbContext CreateDb()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(opts);
    }

    private IConfiguration BuildConfig() =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Key"] = "test_secret_key_at_least_32_chars_long",
            ["Jwt:Issuer"] = "TestIssuer",
            ["Jwt:Audience"] = "TestAudience"
        }).Build();

    [Fact]
    public async Task Register_CreatesUser_ReturnsAccessToken()
    {
        var db = CreateDb();
        var svc = new AuthService(db, BuildConfig(), TimeProvider.System);
        var req = new RegisterRequest("Ana", "ana@test.com", "password123", "America/Sao_Paulo");

        var (response, _) = await svc.RegisterAsync(req);

        Assert.NotNull(response.AccessToken);
        Assert.Equal(3600, response.ExpiresIn);
        Assert.Equal(1, await db.Users.CountAsync());
    }

    [Fact]
    public async Task Register_DuplicateEmail_ThrowsInvalidOperationException()
    {
        var db = CreateDb();
        var svc = new AuthService(db, BuildConfig(), TimeProvider.System);
        var req = new RegisterRequest("Ana", "ana@test.com", "password123", "America/Sao_Paulo");
        await svc.RegisterAsync(req);

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.RegisterAsync(req));
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsTokenAndRefreshCookie()
    {
        var db = CreateDb();
        var svc = new AuthService(db, BuildConfig(), TimeProvider.System);
        await svc.RegisterAsync(new RegisterRequest("Ana", "ana@test.com", "password123", "UTC"));

        var (response, refreshToken) = await svc.LoginAsync(new LoginRequest("ana@test.com", "password123"));

        Assert.NotNull(response.AccessToken);
        Assert.NotEmpty(refreshToken);
    }

    [Fact]
    public async Task Login_InvalidPassword_ThrowsUnauthorizedAccessException()
    {
        var db = CreateDb();
        var svc = new AuthService(db, BuildConfig(), TimeProvider.System);
        await svc.RegisterAsync(new RegisterRequest("Ana", "ana@test.com", "password123", "UTC"));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.LoginAsync(new LoginRequest("ana@test.com", "wrongpassword")));
    }

    [Fact]
    public async Task Refresh_ValidToken_ReturnsNewAccessToken()
    {
        var db = CreateDb();
        var svc = new AuthService(db, BuildConfig(), TimeProvider.System);
        await svc.RegisterAsync(new RegisterRequest("Ana", "ana@test.com", "password123", "UTC"));
        var (_, refreshToken) = await svc.LoginAsync(new LoginRequest("ana@test.com", "password123"));

        var (response, newRefresh) = await svc.RefreshAsync(refreshToken);

        Assert.NotNull(response.AccessToken);
        Assert.NotEmpty(newRefresh);
        Assert.NotEqual(refreshToken, newRefresh);
    }
}
