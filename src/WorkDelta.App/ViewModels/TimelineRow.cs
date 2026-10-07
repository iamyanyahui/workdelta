using WorkDelta.Core.Models;
using WorkDelta.App.Localization;

namespace WorkDelta.App.ViewModels;

public sealed class TimelineRow
{
    public TimelineRow(TimelineEntry entry)
    {
        StartTime = entry.StartedAt.ToLocalTime().ToString("HH:mm");
        EndTime = entry.LastActivityAt.ToLocalTime().ToString("HH:mm");
        Duration = FormatDuration(entry.ActiveDuration);
        Summary = Localizer.Format("TimelineSummary", entry.FileCount, entry.ChangeCount);
        Files = entry.Files.Count == 0
            ? Localizer.Get("NoFileDetails")
            : string.Join("   ·   ", entry.Files.Take(4)) + (entry.Files.Count > 4 ? Localizer.Format("MoreFiles", entry.Files.Count) : string.Empty);
    }

    public string StartTime { get; }
    public string EndTime { get; }
    public string Duration { get; }
    public string Summary { get; }
    public string Files { get; }

    private static string FormatDuration(TimeSpan duration)
    {
        return Localizer.Duration(duration, atLeastOneMinute: true);
    }
}
