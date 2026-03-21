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
