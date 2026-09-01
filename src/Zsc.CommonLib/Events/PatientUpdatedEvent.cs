namespace Zsc.CommonLib.Events;

public sealed record PatientUpdatedEvent(string PatientId, DateTimeOffset UpdatedAtUtc, string ChangeSummary);
