namespace Zsc.CommonLib.Dtos;

// Composed by the BFF from two separate PatientService calls made through
// the Interceptor. The BFF deserializes CommonLib's own PatientDto /
// PatientHistoryEntryDto straight off the wire - a compile-time dependency
// on this library, not just a runtime one.
public sealed record PatientDashboardDto(
    PatientDto Patient,
    IReadOnlyList<PatientHistoryEntryDto> RecentHistory);
