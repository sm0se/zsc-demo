namespace Zsc.CommonLib.Dtos;

public sealed record CreatePatientRequest(
    string MedicalRecordNumber,
    string DisplayName,
    DateOnly DateOfBirth);
