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
