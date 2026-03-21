# Medicine Scheduler — Backend Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build an ASP.NET Core Web API with JWT auth, CRUD for patients/medications, timezone-aware log generation, a 60-second background scheduler job, and VAPID push notifications backed by SQLite.

**Architecture:** Single ASP.NET Core 8 Web API project. A background `IHostedService` runs every 60 seconds executing three ordered steps: dispatch push notifications, auto-skip overdue logs, generate next-day logs. Auth uses short-lived JWT access tokens (returned in response body) plus an HttpOnly refresh token cookie. All scheduling logic is isolated in a `LogGenerationService` that receives a `TimeProvider` for deterministic testing.

**Tech Stack:** .NET 8, ASP.NET Core Web API, EF Core 8 + Sqlite, xUnit, FluentValidation, Serilog, BCrypt.Net-Next, WebPushCSharp, TimeZoneConverter

---

## File Structure

```
MedicineScheduler.sln
src/
└── MedicineScheduler.Api/
    ├── MedicineScheduler.Api.csproj
    ├── Program.cs
    ├── appsettings.json
    ├── appsettings.Development.json
    ├── Data/
    │   └── AppDbContext.cs
    ├── Entities/
    │   ├── User.cs                          enum NotificationPreference
    │   ├── PushSubscription.cs
    │   ├── Patient.cs
    │   ├── Medication.cs
    │   ├── MedicationSchedule.cs
    │   ├── MedicationScheduleSnapshot.cs
    │   └── MedicationLog.cs                 enums LogStatus, SkippedBy
    ├── DTOs/
    │   ├── Auth/RegisterRequest.cs
    │   ├── Auth/LoginRequest.cs
    │   ├── Auth/AuthResponse.cs
    │   ├── Patients/PatientRequest.cs
    │   ├── Patients/PatientResponse.cs
    │   ├── Medications/MedicationRequest.cs
    │   ├── Medications/MedicationResponse.cs
    │   ├── Medications/MedicationScheduleDto.cs
    │   ├── Schedule/ScheduleItemResponse.cs
    │   ├── Schedule/LogActionResponse.cs
    │   ├── Push/SubscribeRequest.cs
    │   ├── Push/UnsubscribeRequest.cs
    │   └── Settings/SettingsDto.cs
    ├── Services/
    │   ├── AuthService.cs                   JWT generation, refresh token, bcrypt
    │   ├── PatientService.cs                CRUD + ownership check
    │   ├── MedicationService.cs             CRUD + snapshot creation + atomic update
    │   ├── LogGenerationService.cs          timezone-aware log creation (pure logic)
    │   ├── ScheduleService.cs               today/date query, confirm, skip
    │   └── PushNotificationService.cs       VAPID push dispatch
    ├── Controllers/
    │   ├── AuthController.cs
    │   ├── PatientsController.cs
    │   ├── MedicationsController.cs
    │   ├── ScheduleController.cs
    │   ├── PushController.cs
    │   └── SettingsController.cs
    ├── Jobs/
    │   └── SchedulerJob.cs                  IHostedService, 60s interval
    ├── Middleware/
    │   └── ExceptionMiddleware.cs
    ├── Validators/
    │   ├── RegisterRequestValidator.cs
    │   ├── LoginRequestValidator.cs
    │   ├── PatientRequestValidator.cs
    │   ├── MedicationRequestValidator.cs
    │   └── SettingsValidator.cs
    └── Extensions/
        └── ClaimsPrincipalExtensions.cs     GetUserId() helper
tests/
└── MedicineScheduler.Tests/
    ├── MedicineScheduler.Tests.csproj
    └── Services/
        ├── LogGenerationServiceTests.cs     timezone, same-day cutoff, EndDate, past StartDate
        ├── MedicationServiceTests.cs        atomic update, snapshot creation, soft delete cleanup
        ├── ScheduleServiceTests.cs          confirm/skip semantics, late overrides
        └── SchedulerJobTests.cs             notification window, auto-skip, daily generation trigger
```

---

### Task 1: Solution and Project Scaffold

**Files:**
- Create: `MedicineScheduler.sln`
- Create: `src/MedicineScheduler.Api/MedicineScheduler.Api.csproj`
- Create: `tests/MedicineScheduler.Tests/MedicineScheduler.Tests.csproj`

- [ ] **Step 1: Create solution and projects**

```bash
dotnet new sln -n MedicineScheduler
dotnet new webapi -n MedicineScheduler.Api -o src/MedicineScheduler.Api --no-openapi
dotnet new xunit -n MedicineScheduler.Tests -o tests/MedicineScheduler.Tests
dotnet sln add src/MedicineScheduler.Api/MedicineScheduler.Api.csproj
dotnet sln add tests/MedicineScheduler.Tests/MedicineScheduler.Tests.csproj
dotnet add tests/MedicineScheduler.Tests reference src/MedicineScheduler.Api
```

- [ ] **Step 2: Add NuGet packages to API project**

```bash
cd src/MedicineScheduler.Api
dotnet add package Microsoft.EntityFrameworkCore.Sqlite
dotnet add package Microsoft.EntityFrameworkCore.Design
dotnet add package Microsoft.EntityFrameworkCore.Tools
dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer
dotnet add package BCrypt.Net-Next
dotnet add package FluentValidation.AspNetCore
dotnet add package Serilog.AspNetCore
dotnet add package Serilog.Sinks.File
dotnet add package WebPushCSharp
dotnet add package TimeZoneConverter
```

- [ ] **Step 3: Add NuGet packages to test project**

```bash
cd tests/MedicineScheduler.Tests
dotnet add package Microsoft.EntityFrameworkCore.InMemory
dotnet add package Moq
```

- [ ] **Step 4: Delete generated boilerplate**

Delete `src/MedicineScheduler.Api/Controllers/WeatherForecastController.cs` and `WeatherForecast.cs`.

- [ ] **Step 5: Verify build**

```bash
dotnet build
```
Expected: Build succeeded, 0 errors.

- [ ] **Step 6: Commit**

```bash
git add .
git commit -m "chore: scaffold solution with API and test projects"
```

---

### Task 2: Entity Models

**Files:**
- Create: `src/MedicineScheduler.Api/Entities/User.cs`
- Create: `src/MedicineScheduler.Api/Entities/PushSubscription.cs`
- Create: `src/MedicineScheduler.Api/Entities/Patient.cs`
- Create: `src/MedicineScheduler.Api/Entities/Medication.cs`
- Create: `src/MedicineScheduler.Api/Entities/MedicationSchedule.cs`
- Create: `src/MedicineScheduler.Api/Entities/MedicationScheduleSnapshot.cs`
- Create: `src/MedicineScheduler.Api/Entities/MedicationLog.cs`

- [ ] **Step 1: Create User entity**

```csharp
// src/MedicineScheduler.Api/Entities/User.cs
namespace MedicineScheduler.Api.Entities;

public enum NotificationPreference { Push, Alarm, Both }

public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Name { get; set; } = "";
    public string Timezone { get; set; } = "UTC";
    public NotificationPreference NotificationPreference { get; set; }
    public bool IsDeleted { get; set; }
    public ICollection<Patient> Patients { get; set; } = [];
    public ICollection<PushSubscription> PushSubscriptions { get; set; } = [];
}
```

- [ ] **Step 2: Create PushSubscription entity**

```csharp
// src/MedicineScheduler.Api/Entities/PushSubscription.cs
namespace MedicineScheduler.Api.Entities;

public class PushSubscription
{
    public Guid Id { get; set; }
    public string Endpoint { get; set; } = "";
    public string P256dhKey { get; set; } = "";
    public string AuthKey { get; set; } = "";
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
}
```

- [ ] **Step 3: Create Patient entity**

```csharp
// src/MedicineScheduler.Api/Entities/Patient.cs
namespace MedicineScheduler.Api.Entities;

public class Patient
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public DateOnly DateOfBirth { get; set; }
    public string? Notes { get; set; }
    public bool IsDeleted { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public ICollection<Medication> Medications { get; set; } = [];
}
```

- [ ] **Step 4: Create Medication entity**

```csharp
// src/MedicineScheduler.Api/Entities/Medication.cs
namespace MedicineScheduler.Api.Entities;

public class Medication
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Dosage { get; set; } = "";
    public string Unit { get; set; } = "";
    public string ApplicationMethod { get; set; } = "";
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public bool IsDeleted { get; set; }
    public Guid PatientId { get; set; }
    public Patient Patient { get; set; } = null!;
    public MedicationSchedule? Schedule { get; set; }
    public ICollection<MedicationScheduleSnapshot> Snapshots { get; set; } = [];
    public ICollection<MedicationLog> Logs { get; set; } = [];
}
```

- [ ] **Step 5: Create MedicationSchedule entity**

```csharp
// src/MedicineScheduler.Api/Entities/MedicationSchedule.cs
namespace MedicineScheduler.Api.Entities;

public class MedicationSchedule
{
    public Guid Id { get; set; }
    public int FrequencyPerDay { get; set; }
    public List<string> Times { get; set; } = [];
    public Guid MedicationId { get; set; }
    public Medication Medication { get; set; } = null!;
}
```

- [ ] **Step 6: Create MedicationScheduleSnapshot entity**

```csharp
// src/MedicineScheduler.Api/Entities/MedicationScheduleSnapshot.cs
namespace MedicineScheduler.Api.Entities;

public class MedicationScheduleSnapshot
{
    public Guid Id { get; set; }
    public int FrequencyPerDay { get; set; }
    public List<string> Times { get; set; } = [];
    public DateTime CreatedAt { get; set; }
    public Guid MedicationId { get; set; }
    public Medication Medication { get; set; } = null!;
}
```

- [ ] **Step 7: Create MedicationLog entity**

```csharp
// src/MedicineScheduler.Api/Entities/MedicationLog.cs
namespace MedicineScheduler.Api.Entities;

public enum LogStatus { Pending, Taken, Skipped }
public enum SkippedBy { Auto, Caregiver }

public class MedicationLog
{
    public Guid Id { get; set; }
    public DateTime ScheduledTime { get; set; }
    public DateTime? TakenAt { get; set; }
    public LogStatus Status { get; set; }
    public SkippedBy? SkippedBy { get; set; }
    public DateTime? NotificationSentAt { get; set; }
    public Guid MedicationId { get; set; }
    public Medication Medication { get; set; } = null!;
    public Guid MedicationScheduleSnapshotId { get; set; }
    public MedicationScheduleSnapshot Snapshot { get; set; } = null!;
}
```

- [ ] **Step 8: Verify build**

```bash
dotnet build
```
Expected: Build succeeded.

- [ ] **Step 9: Commit**

```bash
git add src/MedicineScheduler.Api/Entities/
git commit -m "feat: add entity models"
```

---

### Task 3: EF Core DbContext and Initial Migration

**Files:**
- Create: `src/MedicineScheduler.Api/Data/AppDbContext.cs`

- [ ] **Step 1: Create AppDbContext**

```csharp
// src/MedicineScheduler.Api/Data/AppDbContext.cs
using Microsoft.EntityFrameworkCore;
using MedicineScheduler.Api.Entities;
using System.Text.Json;

namespace MedicineScheduler.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<PushSubscription> PushSubscriptions => Set<PushSubscription>();
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<Medication> Medications => Set<Medication>();
    public DbSet<MedicationSchedule> MedicationSchedules => Set<MedicationSchedule>();
    public DbSet<MedicationScheduleSnapshot> MedicationScheduleSnapshots => Set<MedicationScheduleSnapshot>();
    public DbSet<MedicationLog> MedicationLogs => Set<MedicationLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // User
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email).IsUnique();

        // PushSubscription: unique endpoint per user
        modelBuilder.Entity<PushSubscription>()
            .HasIndex(p => new { p.UserId, p.Endpoint }).IsUnique();

        // MedicationSchedule: unique per medication
        modelBuilder.Entity<MedicationSchedule>()
            .HasIndex(s => s.MedicationId).IsUnique();

        // JSON columns for Times[]
        modelBuilder.Entity<MedicationSchedule>()
            .Property(s => s.Times)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null)!);

        modelBuilder.Entity<MedicationScheduleSnapshot>()
            .Property(s => s.Times)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null)!);

        // Enum storage as string
        modelBuilder.Entity<User>()
            .Property(u => u.NotificationPreference)
            .HasConversion<string>();

        modelBuilder.Entity<MedicationLog>()
            .Property(l => l.Status)
            .HasConversion<string>();

        modelBuilder.Entity<MedicationLog>()
            .Property(l => l.SkippedBy)
            .HasConversion<string>();
    }
}
```

- [ ] **Step 2: Register EF Core and add connection string in appsettings.json**

```json
// src/MedicineScheduler.Api/appsettings.json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=medicine_scheduler.db"
  },
  "Jwt": {
    "Key": "CHANGE_ME_IN_PRODUCTION_32_CHARS_MIN",
    "Issuer": "MedicineScheduler",
    "Audience": "MedicineScheduler"
  },
  "Vapid": {
    "Subject": "mailto:admin@example.com",
    "PublicKey": "",
    "PrivateKey": ""
  },
  "Serilog": {
    "MinimumLevel": "Information",
    "WriteTo": [
      { "Name": "Console" },
      { "Name": "File", "Args": { "path": "logs/log-.txt", "rollingInterval": "Day" } }
    ]
  }
}
```

- [ ] **Step 3: Wire DbContext in Program.cs (minimal)**

Replace the contents of `Program.cs` with a skeleton that will grow across tasks:

```csharp
// src/MedicineScheduler.Api/Program.cs
using Microsoft.EntityFrameworkCore;
using MedicineScheduler.Api.Data;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Host.UseSerilog();
builder.Services.AddControllers();
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();
app.UseHttpsRedirection();
app.MapControllers();
app.Run();
```

- [ ] **Step 4: Create initial migration**

```bash
cd src/MedicineScheduler.Api
dotnet ef migrations add InitialCreate --output-dir Data/Migrations
dotnet ef database update
```
Expected: `medicine_scheduler.db` created.

- [ ] **Step 5: Verify build**

```bash
dotnet build
```

- [ ] **Step 6: Commit**

```bash
git add src/MedicineScheduler.Api/Data/ src/MedicineScheduler.Api/Program.cs src/MedicineScheduler.Api/appsettings.json
git commit -m "feat: add EF Core DbContext and initial migration"
```

---

### Task 4: Auth Service (JWT + Refresh Token + bcrypt)

**Files:**
- Create: `src/MedicineScheduler.Api/Services/AuthService.cs`
- Create: `src/MedicineScheduler.Api/DTOs/Auth/RegisterRequest.cs`
- Create: `src/MedicineScheduler.Api/DTOs/Auth/LoginRequest.cs`
- Create: `src/MedicineScheduler.Api/DTOs/Auth/AuthResponse.cs`
- Test: `tests/MedicineScheduler.Tests/Services/AuthServiceTests.cs`

- [ ] **Step 1: Create DTOs**

```csharp
// src/MedicineScheduler.Api/DTOs/Auth/RegisterRequest.cs
namespace MedicineScheduler.Api.DTOs.Auth;
public record RegisterRequest(string Name, string Email, string Password, string Timezone);
```

```csharp
// src/MedicineScheduler.Api/DTOs/Auth/LoginRequest.cs
namespace MedicineScheduler.Api.DTOs.Auth;
public record LoginRequest(string Email, string Password);
```

```csharp
// src/MedicineScheduler.Api/DTOs/Auth/AuthResponse.cs
namespace MedicineScheduler.Api.DTOs.Auth;
public record AuthResponse(string AccessToken, int ExpiresIn = 3600);
```

- [ ] **Step 2: Write failing tests**

```csharp
// tests/MedicineScheduler.Tests/Services/AuthServiceTests.cs
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
        Assert.NotEqual(refreshToken, newRefresh); // rotated
    }
}
```

- [ ] **Step 3: Run tests — verify they fail**

```bash
dotnet test tests/MedicineScheduler.Tests --filter "FullyQualifiedName~AuthServiceTests"
```
Expected: Compilation error (AuthService not found).

- [ ] **Step 4: Implement AuthService**

```csharp
// src/MedicineScheduler.Api/Services/AuthService.cs
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
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
            claims: [new Claim(ClaimTypes.NameIdentifier, user.Id.ToString())],
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
```

- [ ] **Step 5: Run tests — verify they pass**

```bash
dotnet test tests/MedicineScheduler.Tests --filter "FullyQualifiedName~AuthServiceTests"
```
Expected: 5 tests pass.

- [ ] **Step 6: Commit**

```bash
git add src/MedicineScheduler.Api/DTOs/Auth/ src/MedicineScheduler.Api/Services/AuthService.cs tests/MedicineScheduler.Tests/Services/AuthServiceTests.cs
git commit -m "feat: add AuthService with JWT and refresh token"
```

---

### Task 5: Auth Controller

**Files:**
- Create: `src/MedicineScheduler.Api/Controllers/AuthController.cs`
- Create: `src/MedicineScheduler.Api/Extensions/ClaimsPrincipalExtensions.cs`
- Modify: `src/MedicineScheduler.Api/Program.cs`

- [ ] **Step 1: Create ClaimsPrincipalExtensions**

```csharp
// src/MedicineScheduler.Api/Extensions/ClaimsPrincipalExtensions.cs
using System.Security.Claims;

namespace MedicineScheduler.Api.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal principal) =>
        Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
```

- [ ] **Step 2: Create AuthController**

```csharp
// src/MedicineScheduler.Api/Controllers/AuthController.cs
using Microsoft.AspNetCore.Mvc;
using MedicineScheduler.Api.DTOs.Auth;
using MedicineScheduler.Api.Services;

namespace MedicineScheduler.Api.Controllers;

[ApiController]
[Route("auth")]
public class AuthController(AuthService authService) : ControllerBase
{
    private const string RefreshCookieName = "refreshToken";

    private CookieOptions RefreshCookieOptions => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
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
```

- [ ] **Step 3: Add JWT auth and register AuthService in Program.cs**

Add to `Program.cs` (before `var app = builder.Build();`):

```csharp
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using MedicineScheduler.Api.Services;

// (after builder.Services.AddControllers())
builder.Services.AddScoped<AuthService>();
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)),
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ClockSkew = TimeSpan.Zero
        };
    });
builder.Services.AddAuthorization();
```

Add to `Program.cs` (after `app.UseHttpsRedirection()`):

```csharp
app.UseAuthentication();
app.UseAuthorization();
```

- [ ] **Step 4: Build and verify**

```bash
dotnet build
```

- [ ] **Step 5: Commit**

```bash
git add src/MedicineScheduler.Api/Controllers/AuthController.cs src/MedicineScheduler.Api/Extensions/ src/MedicineScheduler.Api/Program.cs
git commit -m "feat: add AuthController with JWT bearer auth"
```

---

### Task 6: Patient Service and Controller

**Files:**
- Create: `src/MedicineScheduler.Api/DTOs/Patients/PatientRequest.cs`
- Create: `src/MedicineScheduler.Api/DTOs/Patients/PatientResponse.cs`
- Create: `src/MedicineScheduler.Api/Services/PatientService.cs`
- Create: `src/MedicineScheduler.Api/Controllers/PatientsController.cs`

- [ ] **Step 1: Create DTOs**

```csharp
// src/MedicineScheduler.Api/DTOs/Patients/PatientRequest.cs
namespace MedicineScheduler.Api.DTOs.Patients;
public record PatientRequest(string Name, DateOnly DateOfBirth, string? Notes);
```

```csharp
// src/MedicineScheduler.Api/DTOs/Patients/PatientResponse.cs
namespace MedicineScheduler.Api.DTOs.Patients;
public record PatientResponse(Guid Id, string Name, DateOnly DateOfBirth, string? Notes);
```

- [ ] **Step 2: Implement PatientService**

```csharp
// src/MedicineScheduler.Api/Services/PatientService.cs
using MedicineScheduler.Api.Data;
using MedicineScheduler.Api.DTOs.Patients;
using MedicineScheduler.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace MedicineScheduler.Api.Services;

public class PatientService(AppDbContext db)
{
    public async Task<List<PatientResponse>> GetAllAsync(Guid userId) =>
        await db.Patients
            .Where(p => p.UserId == userId && !p.IsDeleted)
            .Select(p => new PatientResponse(p.Id, p.Name, p.DateOfBirth, p.Notes))
            .ToListAsync();

    public async Task<PatientResponse> GetAsync(Guid id, Guid userId)
    {
        var patient = await FindOrThrowAsync(id, userId);
        return new PatientResponse(patient.Id, patient.Name, patient.DateOfBirth, patient.Notes);
    }

    public async Task<PatientResponse> CreateAsync(PatientRequest req, Guid userId)
    {
        var patient = new Patient
        {
            Id = Guid.NewGuid(),
            Name = req.Name,
            DateOfBirth = req.DateOfBirth,
            Notes = req.Notes,
            UserId = userId
        };
        db.Patients.Add(patient);
        await db.SaveChangesAsync();
        return new PatientResponse(patient.Id, patient.Name, patient.DateOfBirth, patient.Notes);
    }

    public async Task<PatientResponse> UpdateAsync(Guid id, PatientRequest req, Guid userId)
    {
        var patient = await FindOrThrowAsync(id, userId);
        patient.Name = req.Name;
        patient.DateOfBirth = req.DateOfBirth;
        patient.Notes = req.Notes;
        await db.SaveChangesAsync();
        return new PatientResponse(patient.Id, patient.Name, patient.DateOfBirth, patient.Notes);
    }

    public async Task DeleteAsync(Guid id, Guid userId)
    {
        var patient = await FindOrThrowAsync(id, userId);
        patient.IsDeleted = true;
        await db.SaveChangesAsync();
    }

    private async Task<Patient> FindOrThrowAsync(Guid id, Guid userId)
    {
        var patient = await db.Patients.SingleOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
        if (patient == null) throw new KeyNotFoundException();
        if (patient.UserId != userId) throw new UnauthorizedAccessException();
        return patient;
    }
}
```

- [ ] **Step 3: Create PatientsController**

```csharp
// src/MedicineScheduler.Api/Controllers/PatientsController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MedicineScheduler.Api.DTOs.Patients;
using MedicineScheduler.Api.Extensions;
using MedicineScheduler.Api.Services;

namespace MedicineScheduler.Api.Controllers;

[ApiController]
[Route("patients")]
[Authorize]
public class PatientsController(PatientService patientService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok(await patientService.GetAllAsync(User.GetUserId()));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id) =>
        Ok(await patientService.GetAsync(id, User.GetUserId()));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] PatientRequest req)
    {
        var response = await patientService.CreateAsync(req, User.GetUserId());
        return StatusCode(201, response);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] PatientRequest req) =>
        Ok(await patientService.UpdateAsync(id, req, User.GetUserId()));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await patientService.DeleteAsync(id, User.GetUserId());
        return NoContent();
    }
}
```

- [ ] **Step 4: Register PatientService in Program.cs**

```csharp
builder.Services.AddScoped<PatientService>();
```

- [ ] **Step 5: Build**

```bash
dotnet build
```

- [ ] **Step 6: Commit**

```bash
git add src/MedicineScheduler.Api/DTOs/Patients/ src/MedicineScheduler.Api/Services/PatientService.cs src/MedicineScheduler.Api/Controllers/PatientsController.cs src/MedicineScheduler.Api/Program.cs
git commit -m "feat: add Patient CRUD service and controller"
```

---

### Task 7: Log Generation Service (Core Logic)

This is the most complex piece. It is extracted as a pure service with no I/O — takes medication data and returns a list of log entries. This makes it fully testable without a database.

**Files:**
- Create: `src/MedicineScheduler.Api/Services/LogGenerationService.cs`
- Test: `tests/MedicineScheduler.Tests/Services/LogGenerationServiceTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
// tests/MedicineScheduler.Tests/Services/LogGenerationServiceTests.cs
using MedicineScheduler.Api.Entities;
using MedicineScheduler.Api.Services;

namespace MedicineScheduler.Tests.Services;

public class LogGenerationServiceTests
{
    private static TimeZoneInfo Brasilia =>
        TimeZoneConverter.TZConvert.GetTimeZoneInfo("America/Sao_Paulo");

    private static MedicationScheduleSnapshot MakeSnapshot(params string[] times) => new()
    {
        Id = Guid.NewGuid(),
        MedicationId = Guid.NewGuid(),
        FrequencyPerDay = times.Length,
        Times = [.. times],
        CreatedAt = DateTime.UtcNow
    };

    private static Medication MakeMedication(Guid snapshotMedicationId, DateOnly? endDate = null) => new()
    {
        Id = snapshotMedicationId,
        Name = "Test",
        Dosage = "10",
        Unit = "mg",
        ApplicationMethod = "oral",
        StartDate = DateOnly.FromDateTime(DateTime.Today),
        EndDate = endDate
    };

    [Fact]
    public void GenerateForDate_FutureDate_AllTimesIncluded()
    {
        var snapshot = MakeSnapshot("08:00", "14:00", "20:00");
        var med = MakeMedication(snapshot.MedicationId);
        var svc = new LogGenerationService();
        var tomorrow = DateOnly.FromDateTime(DateTime.Today.AddDays(1));

        // nowUtc is well in the past relative to tomorrow
        var nowUtc = DateTime.UtcNow;
        var logs = svc.GenerateLogsForDate(med, snapshot, tomorrow, Brasilia, nowUtc, sameDay: false);

        Assert.Equal(3, logs.Count);
    }

    [Fact]
    public void GenerateForDate_SameDay_SkipsPastTimes()
    {
        var snapshot = MakeSnapshot("08:00", "23:59");
        var med = MakeMedication(snapshot.MedicationId);
        var svc = new LogGenerationService();

        // nowUtc is midday in Brasilia (UTC-3) → 15:00 UTC
        // So 08:00 local (11:00 UTC) is in the past; 23:59 local (02:59 UTC next day) is future
        var today = new DateOnly(2026, 3, 21);
        var nowUtc = new DateTime(2026, 3, 21, 15, 0, 0, DateTimeKind.Utc); // 12:00 Brasilia

        var logs = svc.GenerateLogsForDate(med, snapshot, today, Brasilia, nowUtc, sameDay: true);

        Assert.Single(logs); // only 23:59
        Assert.Equal(new DateTime(2026, 3, 22, 2, 59, 0, DateTimeKind.Utc), logs[0].ScheduledTime);
    }

    [Fact]
    public void GenerateForDate_PastStartDate_DoesNotBackfill()
    {
        // If a past date is requested (should not happen in production flow),
        // same-day logic still only includes times after nowUtc
        var snapshot = MakeSnapshot("08:00", "20:00");
        var med = MakeMedication(snapshot.MedicationId);
        var svc = new LogGenerationService();

        var yesterday = new DateOnly(2026, 3, 20);
        var nowUtc = new DateTime(2026, 3, 21, 12, 0, 0, DateTimeKind.Utc);

        var logs = svc.GenerateLogsForDate(med, snapshot, yesterday, Brasilia, nowUtc, sameDay: true);

        Assert.Empty(logs); // all times are in the past
    }

    [Fact]
    public void GenerateForDate_EndDateExcludes_DateAfterEndDate()
    {
        var snapshot = MakeSnapshot("08:00");
        var endDate = new DateOnly(2026, 3, 21);
        var med = MakeMedication(snapshot.MedicationId, endDate: endDate);
        var svc = new LogGenerationService();

        var dayAfterEnd = new DateOnly(2026, 3, 22);
        var nowUtc = new DateTime(2026, 3, 21, 0, 0, 0, DateTimeKind.Utc);

        var logs = svc.GenerateLogsForDate(med, snapshot, dayAfterEnd, Brasilia, nowUtc, sameDay: false);

        Assert.Empty(logs);
    }

    [Fact]
    public void GenerateForDate_EndDateIncludes_DateOnEndDate()
    {
        var snapshot = MakeSnapshot("08:00");
        var endDate = new DateOnly(2026, 3, 21);
        var med = MakeMedication(snapshot.MedicationId, endDate: endDate);
        var svc = new LogGenerationService();

        var nowUtc = new DateTime(2026, 3, 20, 0, 0, 0, DateTimeKind.Utc);

        var logs = svc.GenerateLogsForDate(med, snapshot, endDate, Brasilia, nowUtc, sameDay: false);

        Assert.Single(logs);
    }

    [Fact]
    public void GenerateForDate_ScheduledTimeIsUtc()
    {
        var snapshot = MakeSnapshot("08:00");
        var med = MakeMedication(snapshot.MedicationId);
        var svc = new LogGenerationService();

        var date = new DateOnly(2026, 3, 21);
        var nowUtc = new DateTime(2026, 3, 20, 0, 0, 0, DateTimeKind.Utc);

        var logs = svc.GenerateLogsForDate(med, snapshot, date, Brasilia, nowUtc, sameDay: false);

        // Brasilia is UTC-3: 08:00 local = 11:00 UTC
        Assert.Single(logs);
        Assert.Equal(new DateTime(2026, 3, 21, 11, 0, 0, DateTimeKind.Utc), logs[0].ScheduledTime);
        Assert.Equal(DateTimeKind.Utc, logs[0].ScheduledTime.Kind);
    }
}
```

- [ ] **Step 2: Run tests — verify they fail**

```bash
dotnet test tests/MedicineScheduler.Tests --filter "FullyQualifiedName~LogGenerationServiceTests"
```
Expected: Compilation error (LogGenerationService not found).

- [ ] **Step 3: Implement LogGenerationService**

```csharp
// src/MedicineScheduler.Api/Services/LogGenerationService.cs
using MedicineScheduler.Api.Entities;
using TimeZoneConverter;

namespace MedicineScheduler.Api.Services;

public class LogGenerationService
{
    /// <summary>
    /// Generates MedicationLog entries for a given local calendar date.
    /// If sameDay is true, skips times whose UTC equivalent is at or before nowUtc.
    /// </summary>
    public List<MedicationLog> GenerateLogsForDate(
        Medication medication,
        MedicationScheduleSnapshot snapshot,
        DateOnly date,
        TimeZoneInfo tz,
        DateTime nowUtc,
        bool sameDay = false)
    {
        if (medication.EndDate.HasValue && date > medication.EndDate.Value)
            return [];

        var logs = new List<MedicationLog>();

        foreach (var timeStr in snapshot.Times)
        {
            var time = TimeOnly.ParseExact(timeStr, "HH:mm");
            var localDt = new DateTime(date.Year, date.Month, date.Day,
                time.Hour, time.Minute, 0, DateTimeKind.Unspecified);
            var utcDt = TimeZoneInfo.ConvertTimeToUtc(localDt, tz);

            if (sameDay && utcDt <= nowUtc)
                continue;

            logs.Add(new MedicationLog
            {
                Id = Guid.NewGuid(),
                ScheduledTime = DateTime.SpecifyKind(utcDt, DateTimeKind.Utc),
                Status = LogStatus.Pending,
                MedicationId = medication.Id,
                MedicationScheduleSnapshotId = snapshot.Id
            });
        }

        return logs;
    }

    /// <summary>
    /// Generates logs for the current local date (same-day, from now forward)
    /// and optionally tomorrow if the 23:00 threshold has been crossed.
    /// </summary>
    public List<MedicationLog> GenerateInitialLogs(
        Medication medication,
        MedicationScheduleSnapshot snapshot,
        TimeZoneInfo tz,
        DateTime nowUtc)
    {
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, tz);
        var today = DateOnly.FromDateTime(localNow);

        var logs = GenerateLogsForDate(medication, snapshot, today, tz, nowUtc, sameDay: true);

        if (localNow.Hour >= 23)
        {
            var tomorrow = today.AddDays(1);
            logs.AddRange(GenerateLogsForDate(medication, snapshot, tomorrow, tz, nowUtc, sameDay: false));
        }

        return logs;
    }
}
```

- [ ] **Step 4: Run tests — verify they pass**

```bash
dotnet test tests/MedicineScheduler.Tests --filter "FullyQualifiedName~LogGenerationServiceTests"
```
Expected: 6 tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/MedicineScheduler.Api/Services/LogGenerationService.cs tests/MedicineScheduler.Tests/Services/LogGenerationServiceTests.cs
git commit -m "feat: add LogGenerationService with timezone-aware log generation"
```

---

### Task 8: Medication Service (CRUD + Snapshot + Atomic Update)

**Files:**
- Create: `src/MedicineScheduler.Api/DTOs/Medications/MedicationRequest.cs`
- Create: `src/MedicineScheduler.Api/DTOs/Medications/MedicationResponse.cs`
- Create: `src/MedicineScheduler.Api/DTOs/Medications/MedicationScheduleDto.cs`
- Create: `src/MedicineScheduler.Api/Services/MedicationService.cs`
- Test: `tests/MedicineScheduler.Tests/Services/MedicationServiceTests.cs`

- [ ] **Step 1: Create DTOs**

```csharp
// src/MedicineScheduler.Api/DTOs/Medications/MedicationScheduleDto.cs
namespace MedicineScheduler.Api.DTOs.Medications;
public record MedicationScheduleDto(int FrequencyPerDay, List<string> Times);
```

```csharp
// src/MedicineScheduler.Api/DTOs/Medications/MedicationRequest.cs
namespace MedicineScheduler.Api.DTOs.Medications;
public record MedicationRequest(
    string Name,
    string Dosage,
    string Unit,
    string ApplicationMethod,
    DateOnly StartDate,
    DateOnly? EndDate,
    List<string> Times);
```

```csharp
// src/MedicineScheduler.Api/DTOs/Medications/MedicationResponse.cs
namespace MedicineScheduler.Api.DTOs.Medications;
public record MedicationResponse(
    Guid Id,
    string Name,
    string Dosage,
    string Unit,
    string ApplicationMethod,
    DateOnly StartDate,
    DateOnly? EndDate,
    MedicationScheduleDto Schedule);
```

- [ ] **Step 2: Write failing tests**

```csharp
// tests/MedicineScheduler.Tests/Services/MedicationServiceTests.cs
using Microsoft.EntityFrameworkCore;
using MedicineScheduler.Api.Data;
using MedicineScheduler.Api.DTOs.Medications;
using MedicineScheduler.Api.Entities;
using MedicineScheduler.Api.Services;

namespace MedicineScheduler.Tests.Services;

public class MedicationServiceTests
{
    private (AppDbContext, MedicationService) Setup()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var db = new AppDbContext(opts);
        var logSvc = new LogGenerationService();
        var svc = new MedicationService(db, logSvc, TimeProvider.System);
        return (db, svc);
    }

    private async Task<(Patient, User)> SeedUserAndPatient(AppDbContext db)
    {
        var user = new User
        {
            Id = Guid.NewGuid(), Email = "u@t.com", PasswordHash = "x",
            Name = "User", Timezone = "America/Sao_Paulo"
        };
        var patient = new Patient
        {
            Id = Guid.NewGuid(), Name = "Patient", DateOfBirth = new DateOnly(2000, 1, 1),
            UserId = user.Id, User = user
        };
        db.Users.Add(user);
        db.Patients.Add(patient);
        await db.SaveChangesAsync();
        return (patient, user);
    }

    [Fact]
    public async Task Create_CreatesSnapshotAndLogsAndSchedule()
    {
        var (db, svc) = Setup();
        var (patient, user) = await SeedUserAndPatient(db);
        var req = new MedicationRequest("Losartana", "50", "mg", "oral",
            DateOnly.FromDateTime(DateTime.Today), null, ["08:00", "20:00"]);

        var response = await svc.CreateAsync(patient.Id, req, user.Id);

        Assert.Equal(2, response.Schedule.FrequencyPerDay);
        Assert.Equal(1, await db.MedicationScheduleSnapshots.CountAsync());
        Assert.Equal(1, await db.MedicationSchedules.CountAsync());
    }

    [Fact]
    public async Task Create_FrequencyPerDayDerivedFromTimesCount()
    {
        var (db, svc) = Setup();
        var (patient, user) = await SeedUserAndPatient(db);
        var req = new MedicationRequest("Med", "10", "mg", "oral",
            DateOnly.FromDateTime(DateTime.Today), null, ["08:00", "14:00", "20:00"]);

        var response = await svc.CreateAsync(patient.Id, req, user.Id);

        Assert.Equal(3, response.Schedule.FrequencyPerDay);
    }

    [Fact]
    public async Task Update_CreatesNewSnapshot_DeletesPendingFutureLogs()
    {
        var (db, svc) = Setup();
        var (patient, user) = await SeedUserAndPatient(db);
        var createReq = new MedicationRequest("Med", "10", "mg", "oral",
            DateOnly.FromDateTime(DateTime.Today), null, ["08:00"]);
        var med = await svc.CreateAsync(patient.Id, createReq, user.Id);

        // Manually add a future pending log
        var medEntity = await db.Medications.Include(m => m.Snapshots).FirstAsync();
        db.MedicationLogs.Add(new MedicationLog
        {
            Id = Guid.NewGuid(),
            ScheduledTime = DateTime.UtcNow.AddDays(1),
            Status = LogStatus.Pending,
            MedicationId = medEntity.Id,
            MedicationScheduleSnapshotId = medEntity.Snapshots.First().Id
        });
        await db.SaveChangesAsync();

        var updateReq = new MedicationRequest("Med", "20", "mg", "oral",
            DateOnly.FromDateTime(DateTime.Today), null, ["09:00", "21:00"]);
        await svc.UpdateAsync(med.Id, updateReq, user.Id);

        Assert.Equal(2, await db.MedicationScheduleSnapshots.CountAsync());
        // Pending future log should be deleted
        Assert.DoesNotContain(await db.MedicationLogs.ToListAsync(),
            l => l.Status == LogStatus.Pending && l.ScheduledTime > DateTime.UtcNow);
    }

    [Fact]
    public async Task Delete_SoftDeletes_HardDeletesPendingFutureLogs_RetainsTakenAndSkipped()
    {
        var (db, svc) = Setup();
        var (patient, user) = await SeedUserAndPatient(db);
        var req = new MedicationRequest("Med", "10", "mg", "oral",
            DateOnly.FromDateTime(DateTime.Today), null, ["08:00"]);
        var med = await svc.CreateAsync(patient.Id, req, user.Id);

        var medEntity = await db.Medications.Include(m => m.Snapshots).FirstAsync();
        var snapId = medEntity.Snapshots.First().Id;

        // Add a future pending log (should be hard-deleted)
        db.MedicationLogs.Add(new MedicationLog
        {
            Id = Guid.NewGuid(),
            ScheduledTime = DateTime.UtcNow.AddDays(1),
            Status = LogStatus.Pending,
            MedicationId = medEntity.Id,
            MedicationScheduleSnapshotId = snapId
        });
        // Add a past taken log (should be RETAINED for history)
        var takenLogId = Guid.NewGuid();
        db.MedicationLogs.Add(new MedicationLog
        {
            Id = takenLogId,
            ScheduledTime = DateTime.UtcNow.AddHours(-2),
            Status = LogStatus.Taken,
            TakenAt = DateTime.UtcNow.AddHours(-2),
            MedicationId = medEntity.Id,
            MedicationScheduleSnapshotId = snapId
        });
        await db.SaveChangesAsync();

        await svc.DeleteAsync(med.Id, user.Id);

        var medAfter = await db.Medications.FindAsync(med.Id);
        Assert.True(medAfter!.IsDeleted);
        // Pending future log is gone
        Assert.Empty(await db.MedicationLogs.Where(l =>
            l.MedicationId == med.Id && l.Status == LogStatus.Pending &&
            l.ScheduledTime > DateTime.UtcNow).ToListAsync());
        // Taken log is retained
        Assert.NotNull(await db.MedicationLogs.FindAsync(takenLogId));
    }

    [Fact]
    public async Task Get_CrossUser_ThrowsUnauthorized()
    {
        var (db, svc) = Setup();
        var (patient, user) = await SeedUserAndPatient(db);
        var req = new MedicationRequest("Med", "10", "mg", "oral",
            DateOnly.FromDateTime(DateTime.Today), null, ["08:00"]);
        var med = await svc.CreateAsync(patient.Id, req, user.Id);

        var otherUserId = Guid.NewGuid();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.GetAsync(med.Id, otherUserId));
    }
}
```

- [ ] **Step 3: Run tests — verify they fail**

```bash
dotnet test tests/MedicineScheduler.Tests --filter "FullyQualifiedName~MedicationServiceTests"
```

- [ ] **Step 4: Implement MedicationService**

```csharp
// src/MedicineScheduler.Api/Services/MedicationService.cs
using MedicineScheduler.Api.Data;
using MedicineScheduler.Api.DTOs.Medications;
using MedicineScheduler.Api.Entities;
using Microsoft.EntityFrameworkCore;
using TimeZoneConverter;

namespace MedicineScheduler.Api.Services;

public class MedicationService(AppDbContext db, LogGenerationService logSvc, TimeProvider time)
{
    public async Task<List<MedicationResponse>> GetAllForPatientAsync(Guid patientId, Guid userId)
    {
        await VerifyPatientOwnershipAsync(patientId, userId);
        return await db.Medications
            .Include(m => m.Schedule)
            .Where(m => m.PatientId == patientId && !m.IsDeleted)
            .Select(m => ToResponse(m))
            .ToListAsync();
    }

    public async Task<MedicationResponse> GetAsync(Guid id, Guid userId)
    {
        var med = await FindOrThrowAsync(id, userId);
        await db.Entry(med).Reference(m => m.Schedule).LoadAsync();
        return ToResponse(med);
    }

    public async Task<MedicationResponse> CreateAsync(Guid patientId, MedicationRequest req, Guid userId)
    {
        await VerifyPatientOwnershipAsync(patientId, userId);

        var nowUtc = time.GetUtcNow().UtcDateTime;
        var patient = await db.Patients.Include(p => p.User).SingleAsync(p => p.Id == patientId);
        var tz = TZConvert.GetTimeZoneInfo(patient.User.Timezone);

        var medication = new Medication
        {
            Id = Guid.NewGuid(),
            Name = req.Name,
            Dosage = req.Dosage,
            Unit = req.Unit,
            ApplicationMethod = req.ApplicationMethod,
            StartDate = req.StartDate,
            EndDate = req.EndDate,
            PatientId = patientId
        };

        var snapshot = new MedicationScheduleSnapshot
        {
            Id = Guid.NewGuid(),
            FrequencyPerDay = req.Times.Count,
            Times = req.Times,
            CreatedAt = nowUtc,
            MedicationId = medication.Id
        };

        var schedule = new MedicationSchedule
        {
            Id = Guid.NewGuid(),
            FrequencyPerDay = req.Times.Count,
            Times = req.Times,
            MedicationId = medication.Id
        };

        var logs = logSvc.GenerateInitialLogs(medication, snapshot, tz, nowUtc);

        await using var tx = await db.Database.BeginTransactionAsync();
        db.Medications.Add(medication);
        db.MedicationScheduleSnapshots.Add(snapshot);
        db.MedicationSchedules.Add(schedule);
        db.MedicationLogs.AddRange(logs);
        await db.SaveChangesAsync();
        await tx.CommitAsync();

        medication.Schedule = schedule;
        return ToResponse(medication);
    }

    public async Task<MedicationResponse> UpdateAsync(Guid id, MedicationRequest req, Guid userId)
    {
        var med = await FindOrThrowAsync(id, userId);
        var patient = await db.Patients.Include(p => p.User).SingleAsync(p => p.Id == med.PatientId);
        var tz = TZConvert.GetTimeZoneInfo(patient.User.Timezone);
        var nowUtc = time.GetUtcNow().UtcDateTime;

        await using var tx = await db.Database.BeginTransactionAsync();

        // 1. Create new snapshot
        var snapshot = new MedicationScheduleSnapshot
        {
            Id = Guid.NewGuid(),
            FrequencyPerDay = req.Times.Count,
            Times = req.Times,
            CreatedAt = nowUtc,
            MedicationId = med.Id
        };
        db.MedicationScheduleSnapshots.Add(snapshot);

        // 2. Update medication fields and schedule in-place
        med.Name = req.Name;
        med.Dosage = req.Dosage;
        med.Unit = req.Unit;
        med.ApplicationMethod = req.ApplicationMethod;
        med.StartDate = req.StartDate;
        med.EndDate = req.EndDate;

        var schedule = await db.MedicationSchedules.SingleAsync(s => s.MedicationId == med.Id);
        schedule.FrequencyPerDay = req.Times.Count;
        schedule.Times = req.Times;

        // 3. Delete pending future logs
        var pendingFutureLogs = await db.MedicationLogs
            .Where(l => l.MedicationId == med.Id && l.Status == LogStatus.Pending && l.ScheduledTime > nowUtc)
            .ToListAsync();
        db.MedicationLogs.RemoveRange(pendingFutureLogs);

        await db.SaveChangesAsync();

        // 4. Generate same-day + next-day logs
        var newLogs = logSvc.GenerateInitialLogs(med, snapshot, tz, nowUtc);
        db.MedicationLogs.AddRange(newLogs);
        await db.SaveChangesAsync();

        await tx.CommitAsync();

        med.Schedule = schedule;
        return ToResponse(med);
    }

    public async Task DeleteAsync(Guid id, Guid userId)
    {
        var med = await FindOrThrowAsync(id, userId);
        var nowUtc = time.GetUtcNow().UtcDateTime;

        var pendingFutureLogs = await db.MedicationLogs
            .Where(l => l.MedicationId == id && l.Status == LogStatus.Pending && l.ScheduledTime > nowUtc)
            .ToListAsync();
        db.MedicationLogs.RemoveRange(pendingFutureLogs);

        med.IsDeleted = true;
        await db.SaveChangesAsync();
    }

    private async Task VerifyPatientOwnershipAsync(Guid patientId, Guid userId)
    {
        var patient = await db.Patients.SingleOrDefaultAsync(p => p.Id == patientId && !p.IsDeleted);
        if (patient == null) throw new KeyNotFoundException();
        if (patient.UserId != userId) throw new UnauthorizedAccessException();
    }

    private async Task<Medication> FindOrThrowAsync(Guid id, Guid userId)
    {
        var med = await db.Medications
            .Include(m => m.Patient)
            .SingleOrDefaultAsync(m => m.Id == id && !m.IsDeleted);
        if (med == null) throw new KeyNotFoundException();
        if (med.Patient.UserId != userId) throw new UnauthorizedAccessException();
        return med;
    }

    private static MedicationResponse ToResponse(Medication m) => new(
        m.Id, m.Name, m.Dosage, m.Unit, m.ApplicationMethod, m.StartDate, m.EndDate,
        new MedicationScheduleDto(m.Schedule!.FrequencyPerDay, m.Schedule.Times));
}
```

- [ ] **Step 5: Run tests — verify they pass**

```bash
dotnet test tests/MedicineScheduler.Tests --filter "FullyQualifiedName~MedicationServiceTests"
```
Expected: 5 tests pass.

- [ ] **Step 6: Commit**

```bash
git add src/MedicineScheduler.Api/DTOs/Medications/ src/MedicineScheduler.Api/Services/MedicationService.cs tests/MedicineScheduler.Tests/Services/MedicationServiceTests.cs
git commit -m "feat: add MedicationService with atomic schedule update and snapshot creation"
```

---

### Task 9: Medication Controller

**Files:**
- Create: `src/MedicineScheduler.Api/Controllers/MedicationsController.cs`

- [ ] **Step 1: Create MedicationsController**

```csharp
// src/MedicineScheduler.Api/Controllers/MedicationsController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MedicineScheduler.Api.DTOs.Medications;
using MedicineScheduler.Api.Extensions;
using MedicineScheduler.Api.Services;

namespace MedicineScheduler.Api.Controllers;

[ApiController]
[Authorize]
public class MedicationsController(MedicationService medicationService) : ControllerBase
{
    [HttpGet("patients/{patientId:guid}/medications")]
    public async Task<IActionResult> GetAll(Guid patientId) =>
        Ok(await medicationService.GetAllForPatientAsync(patientId, User.GetUserId()));

    [HttpPost("patients/{patientId:guid}/medications")]
    public async Task<IActionResult> Create(Guid patientId, [FromBody] MedicationRequest req)
    {
        var response = await medicationService.CreateAsync(patientId, req, User.GetUserId());
        return StatusCode(201, response);
    }

    [HttpGet("medications/{id:guid}")]
    public async Task<IActionResult> Get(Guid id) =>
        Ok(await medicationService.GetAsync(id, User.GetUserId()));

    [HttpPut("medications/{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] MedicationRequest req) =>
        Ok(await medicationService.UpdateAsync(id, req, User.GetUserId()));

    [HttpDelete("medications/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await medicationService.DeleteAsync(id, User.GetUserId());
        return NoContent();
    }
}
```

- [ ] **Step 2: Register MedicationService in Program.cs**

```csharp
builder.Services.AddScoped<MedicationService>();
builder.Services.AddSingleton<LogGenerationService>();
```

- [ ] **Step 3: Build**

```bash
dotnet build
```

- [ ] **Step 4: Commit**

```bash
git add src/MedicineScheduler.Api/Controllers/MedicationsController.cs src/MedicineScheduler.Api/Program.cs
git commit -m "feat: add MedicationsController"
```

---

### Task 10: Schedule Service and Controller

**Files:**
- Create: `src/MedicineScheduler.Api/DTOs/Schedule/ScheduleItemResponse.cs`
- Create: `src/MedicineScheduler.Api/DTOs/Schedule/LogActionResponse.cs`
- Create: `src/MedicineScheduler.Api/Services/ScheduleService.cs`
- Create: `src/MedicineScheduler.Api/Controllers/ScheduleController.cs`
- Test: `tests/MedicineScheduler.Tests/Services/ScheduleServiceTests.cs`

- [ ] **Step 1: Create DTOs**

```csharp
// src/MedicineScheduler.Api/DTOs/Schedule/ScheduleItemResponse.cs
using MedicineScheduler.Api.DTOs.Medications;

namespace MedicineScheduler.Api.DTOs.Schedule;

public record PatientSummary(Guid Id, string Name);
public record MedicationSummary(Guid Id, string Name, string Dosage, string Unit, string ApplicationMethod);

public record ScheduleItemResponse(
    Guid LogId,
    DateTime ScheduledTime,
    string ScheduledTimeLocal,
    string Status,
    string? SkippedBy,
    PatientSummary Patient,
    MedicationSummary Medication);
```

```csharp
// src/MedicineScheduler.Api/DTOs/Schedule/LogActionResponse.cs
namespace MedicineScheduler.Api.DTOs.Schedule;
public record LogActionResponse(Guid Id, string Status, DateTime? TakenAt, string? SkippedBy);
```

- [ ] **Step 2: Write failing tests**

```csharp
// tests/MedicineScheduler.Tests/Services/ScheduleServiceTests.cs
using Microsoft.EntityFrameworkCore;
using MedicineScheduler.Api.Data;
using MedicineScheduler.Api.Entities;
using MedicineScheduler.Api.Services;

namespace MedicineScheduler.Tests.Services;

public class ScheduleServiceTests
{
    private AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private async Task<(MedicationLog log, Guid userId)> SeedLogAsync(
        AppDbContext db, LogStatus status = LogStatus.Pending,
        SkippedBy? skippedBy = null)
    {
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId, Email = "u@t.com", PasswordHash = "x",
            Name = "U", Timezone = "UTC"
        };
        var patient = new Patient
        {
            Id = Guid.NewGuid(), Name = "P",
            DateOfBirth = new DateOnly(2000, 1, 1),
            UserId = userId, User = user
        };
        var medication = new Medication
        {
            Id = Guid.NewGuid(), Name = "M", Dosage = "10", Unit = "mg",
            ApplicationMethod = "oral", StartDate = DateOnly.FromDateTime(DateTime.Today),
            PatientId = patient.Id, Patient = patient
        };
        var snapshot = new MedicationScheduleSnapshot
        {
            Id = Guid.NewGuid(), FrequencyPerDay = 1,
            Times = ["08:00"], CreatedAt = DateTime.UtcNow,
            MedicationId = medication.Id, Medication = medication
        };
        var log = new MedicationLog
        {
            Id = Guid.NewGuid(),
            ScheduledTime = DateTime.UtcNow.AddHours(-1),
            Status = status,
            SkippedBy = skippedBy,
            MedicationId = medication.Id, Medication = medication,
            MedicationScheduleSnapshotId = snapshot.Id, Snapshot = snapshot
        };
        db.Users.Add(user);
        db.Patients.Add(patient);
        db.Medications.Add(medication);
        db.MedicationScheduleSnapshots.Add(snapshot);
        db.MedicationLogs.Add(log);
        await db.SaveChangesAsync();
        return (log, userId);
    }

    [Fact]
    public async Task Confirm_Pending_SetsTaken()
    {
        var db = CreateDb();
        var svc = new ScheduleService(db, TimeProvider.System);
        var (log, userId) = await SeedLogAsync(db);

        var result = await svc.ConfirmAsync(log.Id, userId);

        Assert.Equal("taken", result.Status);
        Assert.NotNull(result.TakenAt);
    }

    [Fact]
    public async Task Confirm_AfterAutoSkip_OverridesSkip()
    {
        var db = CreateDb();
        var svc = new ScheduleService(db, TimeProvider.System);
        var (log, userId) = await SeedLogAsync(db, LogStatus.Skipped, SkippedBy.Auto);

        var result = await svc.ConfirmAsync(log.Id, userId);

        Assert.Equal("taken", result.Status);
    }

    [Fact]
    public async Task Skip_Pending_SetsSkippedByCaregiver()
    {
        var db = CreateDb();
        var svc = new ScheduleService(db, TimeProvider.System);
        var (log, userId) = await SeedLogAsync(db);

        var result = await svc.SkipAsync(log.Id, userId);

        Assert.Equal("skipped", result.Status);
        Assert.Equal("caregiver", result.SkippedBy);
    }

    [Fact]
    public async Task Skip_AfterAutoSkip_UpdatesSkippedByToCaregiver()
    {
        var db = CreateDb();
        var svc = new ScheduleService(db, TimeProvider.System);
        var (log, userId) = await SeedLogAsync(db, LogStatus.Skipped, SkippedBy.Auto);

        var result = await svc.SkipAsync(log.Id, userId);

        Assert.Equal("caregiver", result.SkippedBy);
    }

    [Fact]
    public async Task Confirm_CrossUser_ThrowsUnauthorized()
    {
        var db = CreateDb();
        var svc = new ScheduleService(db, TimeProvider.System);
        var (log, _) = await SeedLogAsync(db);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.ConfirmAsync(log.Id, Guid.NewGuid()));
    }
}
```

- [ ] **Step 3: Run tests — verify they fail**

```bash
dotnet test tests/MedicineScheduler.Tests --filter "FullyQualifiedName~ScheduleServiceTests"
```

- [ ] **Step 4: Implement ScheduleService**

```csharp
// src/MedicineScheduler.Api/Services/ScheduleService.cs
using MedicineScheduler.Api.Data;
using MedicineScheduler.Api.DTOs.Schedule;
using MedicineScheduler.Api.Entities;
using Microsoft.EntityFrameworkCore;
using TimeZoneConverter;

namespace MedicineScheduler.Api.Services;

public class ScheduleService(AppDbContext db, TimeProvider time)
{
    public async Task<List<ScheduleItemResponse>> GetForDateAsync(DateOnly date, Guid userId)
    {
        var user = await db.Users.FindAsync(userId) ?? throw new KeyNotFoundException();
        var tz = TZConvert.GetTimeZoneInfo(user.Timezone);

        var startUtc = TimeZoneInfo.ConvertTimeToUtc(
            new DateTime(date.Year, date.Month, date.Day, 0, 0, 0), tz);
        var endUtc = startUtc.AddDays(1);

        var logs = await db.MedicationLogs
            .Include(l => l.Medication).ThenInclude(m => m.Patient)
            .Where(l =>
                l.ScheduledTime >= startUtc &&
                l.ScheduledTime < endUtc &&
                l.Medication.Patient.UserId == userId &&
                !l.Medication.IsDeleted &&
                !l.Medication.Patient.IsDeleted)
            .OrderBy(l => l.ScheduledTime)
            .ToListAsync();

        return logs.Select(l => ToItem(l, tz)).ToList();
    }

    public async Task<LogActionResponse> ConfirmAsync(Guid logId, Guid userId)
    {
        var log = await FindOrThrowAsync(logId, userId);
        log.Status = LogStatus.Taken;
        log.TakenAt = time.GetUtcNow().UtcDateTime;
        log.SkippedBy = null;
        await db.SaveChangesAsync();
        return ToAction(log);
    }

    public async Task<LogActionResponse> SkipAsync(Guid logId, Guid userId)
    {
        var log = await FindOrThrowAsync(logId, userId);
        log.Status = LogStatus.Skipped;
        log.SkippedBy = Entities.SkippedBy.Caregiver;
        await db.SaveChangesAsync();
        return ToAction(log);
    }

    private async Task<MedicationLog> FindOrThrowAsync(Guid logId, Guid userId)
    {
        var log = await db.MedicationLogs
            .Include(l => l.Medication).ThenInclude(m => m.Patient)
            .SingleOrDefaultAsync(l => l.Id == logId);
        if (log == null) throw new KeyNotFoundException();
        if (log.Medication.Patient.UserId != userId) throw new UnauthorizedAccessException();
        return log;
    }

    private static ScheduleItemResponse ToItem(MedicationLog l, TimeZoneInfo tz)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(l.ScheduledTime, tz);
        return new ScheduleItemResponse(
            l.Id,
            l.ScheduledTime,
            local.ToString("HH:mm"),
            l.Status.ToString().ToLower(),
            l.SkippedBy?.ToString().ToLower(),
            new PatientSummary(l.Medication.Patient.Id, l.Medication.Patient.Name),
            new MedicationSummary(l.Medication.Id, l.Medication.Name,
                l.Medication.Dosage, l.Medication.Unit, l.Medication.ApplicationMethod));
    }

    private static LogActionResponse ToAction(MedicationLog l) =>
        new(l.Id, l.Status.ToString().ToLower(), l.TakenAt, l.SkippedBy?.ToString().ToLower());
}
```

- [ ] **Step 5: Create ScheduleController**

```csharp
// src/MedicineScheduler.Api/Controllers/ScheduleController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MedicineScheduler.Api.Extensions;
using MedicineScheduler.Api.Services;

namespace MedicineScheduler.Api.Controllers;

[ApiController]
[Route("schedule")]
[Authorize]
public class ScheduleController(ScheduleService scheduleService, TimeProvider time) : ControllerBase
{
    [HttpGet("today")]
    public async Task<IActionResult> Today()
    {
        var today = DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime);
        return Ok(await scheduleService.GetForDateAsync(today, User.GetUserId()));
    }

    [HttpGet]
    public async Task<IActionResult> ByDate([FromQuery] DateOnly date) =>
        Ok(await scheduleService.GetForDateAsync(date, User.GetUserId()));

    [HttpPost("{logId:guid}/confirm")]
    public async Task<IActionResult> Confirm(Guid logId) =>
        Ok(await scheduleService.ConfirmAsync(logId, User.GetUserId()));

    [HttpPost("{logId:guid}/skip")]
    public async Task<IActionResult> Skip(Guid logId) =>
        Ok(await scheduleService.SkipAsync(logId, User.GetUserId()));
}
```

- [ ] **Step 6: Register ScheduleService in Program.cs**

```csharp
builder.Services.AddScoped<ScheduleService>();
```

- [ ] **Step 7: Run tests — verify they pass**

```bash
dotnet test tests/MedicineScheduler.Tests --filter "FullyQualifiedName~ScheduleServiceTests"
```

- [ ] **Step 8: Commit**

```bash
git add src/MedicineScheduler.Api/DTOs/Schedule/ src/MedicineScheduler.Api/Services/ScheduleService.cs src/MedicineScheduler.Api/Controllers/ScheduleController.cs tests/MedicineScheduler.Tests/Services/ScheduleServiceTests.cs src/MedicineScheduler.Api/Program.cs
git commit -m "feat: add ScheduleService and ScheduleController with confirm/skip"
```

---

### Task 11: Background Scheduler Job

**Files:**
- Create: `src/MedicineScheduler.Api/Services/PushNotificationService.cs`
- Create: `src/MedicineScheduler.Api/Jobs/SchedulerJob.cs`
- Test: `tests/MedicineScheduler.Tests/Services/SchedulerJobTests.cs`

- [ ] **Step 1: Create stub PushNotificationService**

This will be fully implemented in Task 12. For now, use an interface so the job can be tested with a mock.

```csharp
// src/MedicineScheduler.Api/Services/PushNotificationService.cs
using MedicineScheduler.Api.Entities;

namespace MedicineScheduler.Api.Services;

public interface IPushNotificationService
{
    Task SendAsync(User user, string title, string body);
}

public class PushNotificationService(IConfiguration config) : IPushNotificationService
{
    public Task SendAsync(User user, string title, string body)
    {
        // Implemented in Task 12
        return Task.CompletedTask;
    }
}
```

- [ ] **Step 2: Write failing tests for the job**

```csharp
// tests/MedicineScheduler.Tests/Services/SchedulerJobTests.cs
using Microsoft.EntityFrameworkCore;
using MedicineScheduler.Api.Data;
using MedicineScheduler.Api.Entities;
using MedicineScheduler.Api.Jobs;
using MedicineScheduler.Api.Services;
using Moq;

namespace MedicineScheduler.Tests.Services;

public class SchedulerJobTests
{
    private AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    // Wires SchedulerJob to a test-scoped AppDbContext via a real IServiceScopeFactory
    private static SchedulerJob CreateJob(AppDbContext db, IPushNotificationService push, DateTime nowUtc)
    {
        var services = new ServiceCollection();
        services.AddSingleton(db);
        services.AddSingleton(push);
        var sp = services.BuildServiceProvider();
        return new SchedulerJob(
            sp.GetRequiredService<IServiceScopeFactory>(),
            new LogGenerationService(),
            new FakeTimeProvider(nowUtc));
    }

    private async Task<(User user, Medication med, MedicationScheduleSnapshot snap)>
        SeedAsync(AppDbContext db, string timezone = "UTC")
    {
        var user = new User
        {
            Id = Guid.NewGuid(), Email = "u@t.com", PasswordHash = "x",
            Name = "U", Timezone = timezone,
            NotificationPreference = NotificationPreference.Push
        };
        var patient = new Patient
        {
            Id = Guid.NewGuid(), Name = "P", DateOfBirth = new DateOnly(2000, 1, 1),
            UserId = user.Id, User = user
        };
        var med = new Medication
        {
            Id = Guid.NewGuid(), Name = "M", Dosage = "10", Unit = "mg",
            ApplicationMethod = "oral", StartDate = DateOnly.FromDateTime(DateTime.Today),
            PatientId = patient.Id, Patient = patient
        };
        var snap = new MedicationScheduleSnapshot
        {
            Id = Guid.NewGuid(), FrequencyPerDay = 1, Times = ["08:00"],
            CreatedAt = DateTime.UtcNow, MedicationId = med.Id, Medication = med
        };
        db.Users.Add(user);
        db.Patients.Add(patient);
        db.Medications.Add(med);
        db.MedicationScheduleSnapshots.Add(snap);
        await db.SaveChangesAsync();
        return (user, med, snap);
    }

    [Fact]
    public async Task Step1_DispatchesNotification_WhenInWindow()
    {
        var db = CreateDb();
        var pushMock = new Mock<IPushNotificationService>();
        var (user, med, snap) = await SeedAsync(db);
        var nowUtc = new DateTime(2026, 3, 21, 11, 0, 0, DateTimeKind.Utc);

        db.MedicationLogs.Add(new MedicationLog
        {
            Id = Guid.NewGuid(),
            ScheduledTime = nowUtc.AddSeconds(30), // within +2 min window
            Status = LogStatus.Pending,
            MedicationId = med.Id, Medication = med,
            MedicationScheduleSnapshotId = snap.Id, Snapshot = snap
        });
        await db.SaveChangesAsync();

        var job = CreateJob(db, pushMock.Object, nowUtc);
        await job.RunOnceAsync();

        pushMock.Verify(p => p.SendAsync(It.IsAny<User>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Once);
        var log = await db.MedicationLogs.FirstAsync();
        Assert.NotNull(log.NotificationSentAt);
    }

    [Fact]
    public async Task Step1_DoesNotDispatch_WhenAlreadySent()
    {
        var db = CreateDb();
        var pushMock = new Mock<IPushNotificationService>();
        var (user, med, snap) = await SeedAsync(db);
        var nowUtc = new DateTime(2026, 3, 21, 11, 0, 0, DateTimeKind.Utc);

        db.MedicationLogs.Add(new MedicationLog
        {
            Id = Guid.NewGuid(),
            ScheduledTime = nowUtc.AddSeconds(30),
            Status = LogStatus.Pending,
            NotificationSentAt = nowUtc.AddMinutes(-1), // already sent
            MedicationId = med.Id, Medication = med,
            MedicationScheduleSnapshotId = snap.Id, Snapshot = snap
        });
        await db.SaveChangesAsync();

        var job = CreateJob(db, pushMock.Object, nowUtc);
        await job.RunOnceAsync();

        pushMock.Verify(p => p.SendAsync(It.IsAny<User>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task Step2_AutoSkips_OverdueEntries()
    {
        var db = CreateDb();
        var (user, med, snap) = await SeedAsync(db);
        var nowUtc = new DateTime(2026, 3, 21, 11, 0, 0, DateTimeKind.Utc);

        db.MedicationLogs.Add(new MedicationLog
        {
            Id = Guid.NewGuid(),
            ScheduledTime = nowUtc.AddMinutes(-35), // overdue > 30 min
            Status = LogStatus.Pending,
            MedicationId = med.Id, Medication = med,
            MedicationScheduleSnapshotId = snap.Id, Snapshot = snap
        });
        await db.SaveChangesAsync();

        var job = CreateJob(db, Mock.Of<IPushNotificationService>(), nowUtc);
        await job.RunOnceAsync();

        var log = await db.MedicationLogs.FirstAsync();
        Assert.Equal(LogStatus.Skipped, log.Status);
        Assert.Equal(SkippedBy.Auto, log.SkippedBy);
    }

    [Fact]
    public async Task Step3_GeneratesNextDayLogs_WhenAfter23h()
    {
        var db = CreateDb();
        var (user, med, snap) = await SeedAsync(db, timezone: "UTC");
        // 23:30 UTC for a UTC user → triggers next-day generation
        var nowUtc = new DateTime(2026, 3, 21, 23, 30, 0, DateTimeKind.Utc);

        db.MedicationSchedules.Add(new MedicationSchedule
        {
            Id = Guid.NewGuid(), FrequencyPerDay = 1,
            Times = ["08:00"], MedicationId = med.Id
        });
        await db.SaveChangesAsync();

        var job = CreateJob(db, Mock.Of<IPushNotificationService>(), nowUtc);
        await job.RunOnceAsync();

        var tomorrow = new DateOnly(2026, 3, 22);
        var tomorrowLogs = await db.MedicationLogs
            .Where(l => DateOnly.FromDateTime(l.ScheduledTime) == tomorrow)
            .ToListAsync();
        Assert.NotEmpty(tomorrowLogs);
    }
}

// Helper for testing
public class FakeTimeProvider(DateTime fixedUtc) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => new(fixedUtc, TimeSpan.Zero);
}

// Creates a SchedulerJob wired to a real AppDbContext via a scope factory stub
internal static class SchedulerJobFactory
{
    public static SchedulerJob Create(AppDbContext db, IPushNotificationService push, DateTime nowUtc)
    {
        var services = new ServiceCollection();
        services.AddSingleton(db);
        services.AddSingleton(push);
        var sp = services.BuildServiceProvider();
        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
        return new SchedulerJob(scopeFactory, new LogGenerationService(), new FakeTimeProvider(nowUtc));
    }
}

// In the test class, add this helper:
// private static SchedulerJob CreateJob(AppDbContext db, IPushNotificationService push, DateTime nowUtc)
//     => SchedulerJobFactory.Create(db, push, nowUtc);
```

- [ ] **Step 3: Run tests — verify they fail**

```bash
dotnet test tests/MedicineScheduler.Tests --filter "FullyQualifiedName~SchedulerJobTests"
```

- [ ] **Step 4: Implement SchedulerJob**

`SchedulerJob` is a singleton `BackgroundService`. Because `AppDbContext` and `IPushNotificationService` are scoped, it uses `IServiceScopeFactory` to create a scope on each run.

```csharp
// src/MedicineScheduler.Api/Jobs/SchedulerJob.cs
using MedicineScheduler.Api.Data;
using MedicineScheduler.Api.Entities;
using MedicineScheduler.Api.Services;
using Microsoft.EntityFrameworkCore;
using TimeZoneConverter;

namespace MedicineScheduler.Api.Jobs;

public class SchedulerJob(
    IServiceScopeFactory scopeFactory,
    LogGenerationService logGenerationService,
    TimeProvider time) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await RunOnceAsync(); }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[SchedulerJob] Error: {ex.Message}");
            }
            await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
        }
    }

    public async Task RunOnceAsync()
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var pushService = scope.ServiceProvider.GetRequiredService<IPushNotificationService>();
        var nowUtc = time.GetUtcNow().UtcDateTime;

        await DispatchNotificationsAsync(db, pushService, nowUtc);
        await AutoSkipOverdueAsync(db, nowUtc);
        await GenerateNextDayLogsAsync(db, nowUtc);
    }

    private async Task DispatchNotificationsAsync(
        AppDbContext db, IPushNotificationService pushService, DateTime nowUtc)
    {
        var windowStart = nowUtc.AddMinutes(-1);
        var windowEnd = nowUtc.AddMinutes(2);

        var pendingLogs = await db.MedicationLogs
            .Include(l => l.Medication).ThenInclude(m => m.Patient).ThenInclude(p => p.User)
            .Include(l => l.Snapshot)
            .Where(l =>
                l.ScheduledTime >= windowStart &&
                l.ScheduledTime <= windowEnd &&
                l.NotificationSentAt == null &&
                l.Status == LogStatus.Pending &&
                !l.Medication.IsDeleted &&
                !l.Medication.Patient.IsDeleted)
            .ToListAsync();

        foreach (var log in pendingLogs)
        {
            var user = log.Medication.Patient.User;
            if (user.NotificationPreference == NotificationPreference.Alarm) continue;

            await pushService.SendAsync(user,
                $"Hora do medicamento: {log.Medication.Name}",
                $"{log.Medication.Dosage} {log.Medication.Unit} — {log.Medication.ApplicationMethod}");

            log.NotificationSentAt = nowUtc;
        }

        await db.SaveChangesAsync();
    }

    private static async Task AutoSkipOverdueAsync(AppDbContext db, DateTime nowUtc)
    {
        var cutoff = nowUtc.AddMinutes(-30);

        var overdue = await db.MedicationLogs
            .Where(l =>
                l.Status == LogStatus.Pending &&
                l.ScheduledTime < cutoff &&
                !l.Medication.IsDeleted)
            .ToListAsync();

        foreach (var log in overdue)
        {
            log.Status = LogStatus.Skipped;
            log.SkippedBy = SkippedBy.Auto;
        }

        await db.SaveChangesAsync();
    }

    private async Task GenerateNextDayLogsAsync(AppDbContext db, DateTime nowUtc)
    {
        var users = await db.Users.Where(u => !u.IsDeleted).ToListAsync();

        foreach (var user in users)
        {
            var tz = TZConvert.GetTimeZoneInfo(user.Timezone);
            var localNow = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, tz);
            if (localNow.Hour < 23) continue;

            var tomorrow = DateOnly.FromDateTime(localNow).AddDays(1);

            var alreadyHasLogs = await db.MedicationLogs
                .Include(l => l.Medication).ThenInclude(m => m.Patient)
                .AnyAsync(l =>
                    l.Medication.Patient.UserId == user.Id &&
                    DateOnly.FromDateTime(l.ScheduledTime) == tomorrow);

            if (alreadyHasLogs) continue;

            var medications = await db.Medications
                .Include(m => m.Schedule)
                .Include(m => m.Snapshots.OrderByDescending(s => s.CreatedAt))
                .Where(m => m.Patient.UserId == user.Id && !m.IsDeleted)
                .ToListAsync();

            var newLogs = new List<MedicationLog>();
            foreach (var med in medications.Where(m => m.Schedule != null))
            {
                var snapshot = med.Snapshots.First(); // already ordered descending
                newLogs.AddRange(logGenerationService.GenerateLogsForDate(
                    med, snapshot, tomorrow, tz, nowUtc, sameDay: false));
            }

            db.MedicationLogs.AddRange(newLogs);
        }

        await db.SaveChangesAsync();
    }
}
```

- [ ] **Step 5: Run tests — verify they pass**

```bash
dotnet test tests/MedicineScheduler.Tests --filter "FullyQualifiedName~SchedulerJobTests"
```

- [ ] **Step 6: Register SchedulerJob in Program.cs**

`SchedulerJob` is registered as a hosted service. `IPushNotificationService` will be registered as scoped in Task 12 — for now add a stub singleton that does nothing:

```csharp
builder.Services.AddSingleton<IPushNotificationService, PushNotificationService>();
builder.Services.AddHostedService<SchedulerJob>();
```

> **Note:** In Task 12 this will change to `AddScoped<IPushNotificationService, PushNotificationService>()`. The job uses `IServiceScopeFactory` so it handles scoped services correctly.

- [ ] **Step 7: Commit**

```bash
git add src/MedicineScheduler.Api/Services/PushNotificationService.cs src/MedicineScheduler.Api/Jobs/SchedulerJob.cs tests/MedicineScheduler.Tests/Services/SchedulerJobTests.cs src/MedicineScheduler.Api/Program.cs
git commit -m "feat: add SchedulerJob with notification dispatch, auto-skip, and next-day log generation"
```

---

### Task 12: Push Notification Service and Controller

**Files:**
- Modify: `src/MedicineScheduler.Api/Services/PushNotificationService.cs`
- Create: `src/MedicineScheduler.Api/DTOs/Push/SubscribeRequest.cs`
- Create: `src/MedicineScheduler.Api/DTOs/Push/UnsubscribeRequest.cs`
- Create: `src/MedicineScheduler.Api/Controllers/PushController.cs`

- [ ] **Step 1: Implement PushNotificationService with VAPID**

```csharp
// src/MedicineScheduler.Api/Services/PushNotificationService.cs
using MedicineScheduler.Api.Entities;
using WebPush;
using System.Text.Json;

namespace MedicineScheduler.Api.Services;

public interface IPushNotificationService
{
    Task SendAsync(User user, string title, string body);
}

public class PushNotificationService(AppDbContext db, IConfiguration config) : IPushNotificationService
{
    public async Task SendAsync(User user, string title, string body)
    {
        var vapidSubject = config["Vapid:Subject"]!;
        var vapidPublicKey = config["Vapid:PublicKey"]!;
        var vapidPrivateKey = config["Vapid:PrivateKey"]!;

        var subs = await db.PushSubscriptions
            .Where(s => s.UserId == user.Id)
            .ToListAsync();

        var payload = JsonSerializer.Serialize(new { title, body });

        foreach (var sub in subs)
        {
            var pushSub = new PushSubscription(sub.Endpoint, sub.P256dhKey, sub.AuthKey);
            var vapidDetails = new VapidDetails(vapidSubject, vapidPublicKey, vapidPrivateKey);
            var client = new WebPushClient();
            try
            {
                await client.SendNotificationAsync(pushSub, payload, vapidDetails);
            }
            catch (WebPushException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Gone)
            {
                // Subscription expired — remove it
                db.PushSubscriptions.Remove(sub);
            }
        }

        await db.SaveChangesAsync();
    }
}
```

- [ ] **Step 2: Create DTOs and controller**

```csharp
// src/MedicineScheduler.Api/DTOs/Push/SubscribeRequest.cs
namespace MedicineScheduler.Api.DTOs.Push;
public record SubscribeRequest(string Endpoint, string P256dh, string Auth);
```

```csharp
// src/MedicineScheduler.Api/DTOs/Push/UnsubscribeRequest.cs
namespace MedicineScheduler.Api.DTOs.Push;
public record UnsubscribeRequest(string Endpoint);
```

```csharp
// src/MedicineScheduler.Api/Controllers/PushController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MedicineScheduler.Api.Data;
using MedicineScheduler.Api.DTOs.Push;
using MedicineScheduler.Api.Entities;
using MedicineScheduler.Api.Extensions;
using Microsoft.EntityFrameworkCore;

namespace MedicineScheduler.Api.Controllers;

[ApiController]
[Route("push")]
[Authorize]
public class PushController(AppDbContext db) : ControllerBase
{
    [HttpPost("subscribe")]
    public async Task<IActionResult> Subscribe([FromBody] SubscribeRequest req)
    {
        var userId = User.GetUserId();
        var existing = await db.PushSubscriptions
            .SingleOrDefaultAsync(s => s.UserId == userId && s.Endpoint == req.Endpoint);

        if (existing != null)
        {
            existing.P256dhKey = req.P256dh;
            existing.AuthKey = req.Auth;
            await db.SaveChangesAsync();
            return Ok();
        }

        db.PushSubscriptions.Add(new PushSubscription
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Endpoint = req.Endpoint,
            P256dhKey = req.P256dh,
            AuthKey = req.Auth
        });
        await db.SaveChangesAsync();
        return StatusCode(201);
    }

    [HttpPost("unsubscribe")]
    public async Task<IActionResult> Unsubscribe([FromBody] UnsubscribeRequest req)
    {
        var userId = User.GetUserId();
        var sub = await db.PushSubscriptions
            .SingleOrDefaultAsync(s => s.UserId == userId && s.Endpoint == req.Endpoint);

        if (sub != null)
        {
            db.PushSubscriptions.Remove(sub);
            await db.SaveChangesAsync();
        }

        return NoContent();
    }
}
```

- [ ] **Step 3: Update PushNotificationService registration in Program.cs**

Replace the stub registration with the real one (pass `db` via DI — it's already scoped, so use `IServiceProvider`):

```csharp
// PushNotificationService needs AppDbContext which is scoped.
// Register as Scoped instead of Singleton:
builder.Services.AddScoped<IPushNotificationService, PushNotificationService>();
// Remove the Singleton registration added in Task 11
```

> **Note:** Because `SchedulerJob` is a singleton `BackgroundService` that needs `IPushNotificationService` (now scoped), inject `IServiceScopeFactory` into `SchedulerJob` instead of `IPushNotificationService` directly. Update `SchedulerJob` constructor:

```csharp
public class SchedulerJob(
    IServiceScopeFactory scopeFactory,
    LogGenerationService logGenerationService,
    TimeProvider time) : BackgroundService
{
    public async Task RunOnceAsync()
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var pushService = scope.ServiceProvider.GetRequiredService<IPushNotificationService>();
        var nowUtc = time.GetUtcNow().UtcDateTime;
        await DispatchNotificationsAsync(db, pushService, nowUtc);
        await AutoSkipOverdueAsync(db, nowUtc);
        await GenerateNextDayLogsAsync(db, nowUtc);
    }
    // Update private methods to accept db and pushService parameters
}
```

Update test helper `new SchedulerJob(db, pushMock.Object, ...)` to use a real scope factory by creating a ServiceProvider in tests, or alternatively keep a test constructor overload.

- [ ] **Step 4: Build**

```bash
dotnet build
```

- [ ] **Step 5: Commit**

```bash
git add src/MedicineScheduler.Api/Services/PushNotificationService.cs src/MedicineScheduler.Api/DTOs/Push/ src/MedicineScheduler.Api/Controllers/PushController.cs src/MedicineScheduler.Api/Jobs/SchedulerJob.cs src/MedicineScheduler.Api/Program.cs
git commit -m "feat: add VAPID push notification service and push controller"
```

---

### Task 13: Settings Controller and Timezone Change

**Files:**
- Create: `src/MedicineScheduler.Api/DTOs/Settings/SettingsDto.cs`
- Create: `src/MedicineScheduler.Api/Controllers/SettingsController.cs`

- [ ] **Step 1: Create SettingsDto**

```csharp
// src/MedicineScheduler.Api/DTOs/Settings/SettingsDto.cs
namespace MedicineScheduler.Api.DTOs.Settings;
public record SettingsDto(string NotificationPreference, string Timezone);
```

- [ ] **Step 2: Create SettingsController**

```csharp
// src/MedicineScheduler.Api/Controllers/SettingsController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MedicineScheduler.Api.Data;
using MedicineScheduler.Api.DTOs.Settings;
using MedicineScheduler.Api.Entities;
using MedicineScheduler.Api.Extensions;
using Microsoft.EntityFrameworkCore;
using TimeZoneConverter;

namespace MedicineScheduler.Api.Controllers;

[ApiController]
[Route("settings")]
[Authorize]
public class SettingsController(AppDbContext db, TimeProvider time) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var user = await db.Users.FindAsync(User.GetUserId());
        if (user == null) return NotFound();
        return Ok(new SettingsDto(user.NotificationPreference.ToString().ToLower(), user.Timezone));
    }

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] SettingsDto req)
    {
        var userId = User.GetUserId();
        var user = await db.Users.FindAsync(userId);
        if (user == null) return NotFound();

        var timezoneChanged = user.Timezone != req.Timezone;
        user.NotificationPreference = Enum.Parse<NotificationPreference>(req.NotificationPreference, ignoreCase: true);
        user.Timezone = req.Timezone;

        if (timezoneChanged)
        {
            var nowUtc = time.GetUtcNow().UtcDateTime;
            var newTz = TZConvert.GetTimeZoneInfo(req.Timezone);

            // All log regeneration in a single transaction (spec requirement)
            await using var tx = await db.Database.BeginTransactionAsync();

            // Delete all pending future logs for this user
            var futurePending = await db.MedicationLogs
                .Include(l => l.Medication).ThenInclude(m => m.Patient)
                .Where(l =>
                    l.Medication.Patient.UserId == userId &&
                    l.Status == LogStatus.Pending &&
                    l.ScheduledTime > nowUtc)
                .ToListAsync();
            db.MedicationLogs.RemoveRange(futurePending);

            // Regenerate using new timezone
            var medications = await db.Medications
                .Include(m => m.Schedule)
                .Include(m => m.Snapshots.OrderByDescending(s => s.CreatedAt))
                .Where(m => m.Patient.UserId == userId && !m.IsDeleted)
                .ToListAsync();

            var logSvc = new Services.LogGenerationService();
            foreach (var med in medications.Where(m => m.Schedule != null))
            {
                var snapshot = med.Snapshots.First(); // already ordered descending
                var newLogs = logSvc.GenerateInitialLogs(med, snapshot, newTz, nowUtc);
                db.MedicationLogs.AddRange(newLogs);
            }

            await db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        else
        {
            await db.SaveChangesAsync();
        }
        return Ok(new SettingsDto(user.NotificationPreference.ToString().ToLower(), user.Timezone));
    }
}
```

- [ ] **Step 3: Build**

```bash
dotnet build
```

- [ ] **Step 4: Commit**

```bash
git add src/MedicineScheduler.Api/DTOs/Settings/ src/MedicineScheduler.Api/Controllers/SettingsController.cs
git commit -m "feat: add SettingsController with timezone change and log regeneration"
```

---

### Task 14: Exception Middleware, Validation, and Final Program.cs Wiring

**Files:**
- Create: `src/MedicineScheduler.Api/Middleware/ExceptionMiddleware.cs`
- Create: `src/MedicineScheduler.Api/Validators/RegisterRequestValidator.cs`
- Create: `src/MedicineScheduler.Api/Validators/PatientRequestValidator.cs`
- Create: `src/MedicineScheduler.Api/Validators/MedicationRequestValidator.cs`
- Create: `src/MedicineScheduler.Api/Validators/SettingsValidator.cs`
- Modify: `src/MedicineScheduler.Api/Program.cs`

- [ ] **Step 1: Create ExceptionMiddleware**

```csharp
// src/MedicineScheduler.Api/Middleware/ExceptionMiddleware.cs
using System.Text.Json;

namespace MedicineScheduler.Api.Middleware;

public class ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (KeyNotFoundException)
        {
            context.Response.StatusCode = 404;
            await WriteJson(context, new { error = "Not found." });
        }
        catch (UnauthorizedAccessException)
        {
            context.Response.StatusCode = 403;
            await WriteJson(context, new { error = "Access denied." });
        }
        catch (InvalidOperationException ex)
        {
            context.Response.StatusCode = 409;
            await WriteJson(context, new { error = ex.Message });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception");
            context.Response.StatusCode = 500;
            await WriteJson(context, new { error = "An unexpected error occurred." });
        }
    }

    private static Task WriteJson(HttpContext ctx, object body)
    {
        ctx.Response.ContentType = "application/json";
        return ctx.Response.WriteAsync(JsonSerializer.Serialize(body));
    }
}
```

- [ ] **Step 2: Create validators**

```csharp
// src/MedicineScheduler.Api/Validators/RegisterRequestValidator.cs
using FluentValidation;
using MedicineScheduler.Api.DTOs.Auth;

namespace MedicineScheduler.Api.Validators;

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8).MaximumLength(72);
        RuleFor(x => x.Name).NotEmpty();
        RuleFor(x => x.Timezone).NotEmpty();
    }
}
```

```csharp
// src/MedicineScheduler.Api/Validators/PatientRequestValidator.cs
using FluentValidation;
using MedicineScheduler.Api.DTOs.Patients;

namespace MedicineScheduler.Api.Validators;

public class PatientRequestValidator : AbstractValidator<PatientRequest>
{
    public PatientRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.DateOfBirth).NotEmpty().LessThan(DateOnly.FromDateTime(DateTime.Today));
    }
}
```

```csharp
// src/MedicineScheduler.Api/Validators/MedicationRequestValidator.cs
using FluentValidation;
using MedicineScheduler.Api.DTOs.Medications;
using System.Text.RegularExpressions;

namespace MedicineScheduler.Api.Validators;

public partial class MedicationRequestValidator : AbstractValidator<MedicationRequest>
{
    [GeneratedRegex(@"^\d{2}:\d{2}$")]
    private static partial Regex HhMmRegex();

    public MedicationRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Dosage).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Unit).NotEmpty().MaximumLength(50);
        RuleFor(x => x.ApplicationMethod).NotEmpty().MaximumLength(100);
        RuleFor(x => x.StartDate).NotEmpty();
        RuleFor(x => x).Must(x => x.EndDate == null || x.StartDate <= x.EndDate)
            .WithName("EndDate").WithMessage("EndDate must be >= StartDate.");
        RuleFor(x => x.Times).NotEmpty()
            .Must(t => t.Count >= 1 && t.Count <= 24).WithMessage("Times must have 1–24 entries.")
            .Must(t => t.All(s => HhMmRegex().IsMatch(s))).WithMessage("Each time must be HH:mm.")
            .Must(t => t.Distinct().Count() == t.Count).WithMessage("Times must be unique.");
    }
}
```

```csharp
// src/MedicineScheduler.Api/Validators/SettingsValidator.cs
using FluentValidation;
using MedicineScheduler.Api.DTOs.Settings;
using MedicineScheduler.Api.Entities;
using TimeZoneConverter;

namespace MedicineScheduler.Api.Validators;

public class SettingsValidator : AbstractValidator<SettingsDto>
{
    private static readonly string[] ValidPreferences =
        Enum.GetNames<NotificationPreference>().Select(n => n.ToLower()).ToArray();

    public SettingsValidator()
    {
        RuleFor(x => x.NotificationPreference)
            .Must(p => ValidPreferences.Contains(p.ToLower()))
            .WithMessage($"Must be one of: {string.Join(", ", ValidPreferences)}.");
        RuleFor(x => x.Timezone)
            .Must(tz => { try { TZConvert.GetTimeZoneInfo(tz); return true; } catch { return false; } })
            .WithMessage("Invalid IANA timezone.");
    }
}
```

- [ ] **Step 3: Finalize Program.cs**

Replace the entire `Program.cs` with the complete wiring:

```csharp
using System.Text;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MedicineScheduler.Api.Data;
using MedicineScheduler.Api.Jobs;
using MedicineScheduler.Api.Middleware;
using MedicineScheduler.Api.Services;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();
builder.Host.UseSerilog();

builder.Services.AddControllers();
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)),
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ClockSkew = TimeSpan.Zero
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<LogGenerationService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<PatientService>();
builder.Services.AddScoped<MedicationService>();
builder.Services.AddScoped<ScheduleService>();
builder.Services.AddScoped<IPushNotificationService, PushNotificationService>();
builder.Services.AddHostedService<SchedulerJob>();

var app = builder.Build();

app.UseMiddleware<ExceptionMiddleware>();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Auto-apply migrations on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

app.Run();
```

- [ ] **Step 4: Run all tests**

```bash
dotnet test
```
Expected: All tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/MedicineScheduler.Api/Middleware/ src/MedicineScheduler.Api/Validators/ src/MedicineScheduler.Api/Program.cs
git commit -m "feat: add exception middleware, FluentValidation validators, and finalize DI wiring"
```

---

## Summary

| Task | What it delivers |
|------|-----------------|
| 1 | Solution scaffold, packages |
| 2 | All entity models |
| 3 | EF Core DbContext, migration, SQLite |
| 4 | AuthService: register, login, refresh, bcrypt, JWT |
| 5 | AuthController: cookie transport |
| 6 | PatientService + PatientsController |
| 7 | LogGenerationService (timezone-aware, pure logic) |
| 8 | MedicationService: CRUD, snapshots, atomic update |
| 9 | MedicationsController |
| 10 | ScheduleService: today view, confirm/skip semantics |
| 11 | SchedulerJob: notify → auto-skip → daily gen |
| 12 | PushNotificationService (VAPID) + PushController |
| 13 | SettingsController + timezone regeneration |
| 14 | ExceptionMiddleware, validators, final wiring |
