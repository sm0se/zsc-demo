namespace Zsc.CommonLib.Dtos;

public sealed record PatientDto(
    string PatientId,
    string MedicalRecordNumber,
    string DisplayName,
    DateOnly DateOfBirth,
    int CaseCount);
