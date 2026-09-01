using Zsc.PatientService.Models;

namespace Zsc.PatientService.Data;

public interface IPatientRepository
{
    Patient? Get(string patientId);
    Patient Create(string medicalRecordNumber, string displayName, DateOnly dateOfBirth);
    Patient? AddHistory(string patientId, string description);
    IReadOnlyList<Patient> All();
}
