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
