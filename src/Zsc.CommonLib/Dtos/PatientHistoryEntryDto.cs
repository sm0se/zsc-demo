namespace Zsc.CommonLib.Dtos;

public sealed record PatientHistoryEntryDto(
    string PatientId,
    DateTimeOffset OccurredAtUtc,
    string Description);
