namespace MedicineScheduler.Api.Entities;

public class MedicationSchedule
{
    public Guid Id { get; set; }
    public int FrequencyPerDay { get; set; }
    public List<string> Times { get; set; } = [];
    public Guid MedicationId { get; set; }
    public Medication Medication { get; set; } = null!;
}
