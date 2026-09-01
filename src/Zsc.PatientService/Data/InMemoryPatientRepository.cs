using System.Collections.Concurrent;
using Zsc.PatientService.Models;

namespace Zsc.PatientService.Data;

// Stands in for a real datastore. Seeded with a couple of sample patients so
// the demo (and the gRPC integration test) has something to read on first run.
public sealed class InMemoryPatientRepository : IPatientRepository
{
    private readonly ConcurrentDictionary<string, Patient> _patients = new();
    private int _nextId = 1;

    public InMemoryPatientRepository()
    {
        SeedPatient("Amara Osei", new DateOnly(1978, 4, 12));
        SeedPatient("Rohan Mehta", new DateOnly(1985, 11, 2));
    }

    private void SeedPatient(string displayName, DateOnly dateOfBirth)
    {
        var patient = Create($"MRN-{_nextId:D5}", displayName, dateOfBirth);
        AddHistory(patient.PatientId, "Initial case record created during onboarding.");
    }

    public Patient? Get(string patientId) => _patients.GetValueOrDefault(patientId);

    public Patient Create(string medicalRecordNumber, string displayName, DateOnly dateOfBirth)
    {
        var patient = new Patient
        {
            PatientId = $"pat-{_nextId++:D6}",
            MedicalRecordNumber = medicalRecordNumber,
            DisplayName = displayName,
            DateOfBirth = dateOfBirth,
        };
        _patients[patient.PatientId] = patient;
        return patient;
    }

    public Patient? AddHistory(string patientId, string description)
    {
        if (!_patients.TryGetValue(patientId, out var patient)) return null;
        patient.History.Add(new PatientHistoryEntry(DateTimeOffset.UtcNow, description));
        return patient;
    }

    public IReadOnlyList<Patient> All() => _patients.Values.ToList();
}
