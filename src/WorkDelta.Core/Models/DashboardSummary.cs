namespace WorkDelta.Core.Models;
public sealed record DashboardSummary(int SessionCount, int FileCount, int ChangeCount, TimeSpan ActiveDuration);
