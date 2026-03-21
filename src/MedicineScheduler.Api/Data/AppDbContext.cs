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
