namespace Zsc.PatientService.Models;

public sealed class Patient
{
    public required string PatientId { get; init; }
    public required string MedicalRecordNumber { get; init; }
    public required string DisplayName { get; init; }
    public required DateOnly DateOfBirth { get; init; }
    public List<PatientHistoryEntry> History { get; } = new();
}

public sealed record PatientHistoryEntry(DateTimeOffset OccurredAtUtc, string Description);
