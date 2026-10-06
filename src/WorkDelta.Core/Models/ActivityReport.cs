namespace WorkDelta.Core.Models;

public sealed record ProjectActivityReport(
    string ProjectId,
    string ProjectName,
    DateOnly StartDate,
    DateOnly EndDate,
    int SessionCount,
    int FileCount,
    int ChangeCount,
    TimeSpan ActiveDuration);

public sealed record ProjectPreferences(string ProjectId, string IgnorePatterns);
