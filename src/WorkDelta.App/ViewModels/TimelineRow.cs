using WorkDelta.Core.Models;

namespace WorkDelta.App.ViewModels;

public sealed class TimelineRow
{
    public TimelineRow(TimelineEntry entry)
    {
        StartTime = entry.StartedAt.ToLocalTime().ToString("HH:mm");
        EndTime = entry.LastActivityAt.ToLocalTime().ToString("HH:mm");
        Duration = FormatDuration(entry.ActiveDuration);
        Summary = $"修改 {entry.FileCount} 个文件 · {entry.ChangeCount} 次有效变化";
        Files = entry.Files.Count == 0
            ? "暂无文件明细"
            : string.Join("   ·   ", entry.Files.Take(4)) + (entry.Files.Count > 4 ? $"   等 {entry.Files.Count} 个文件" : string.Empty);
    }

    public string StartTime { get; }
    public string EndTime { get; }
    public string Duration { get; }
    public string Summary { get; }
    public string Files { get; }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalHours >= 1)
        {
            return $"{(int)duration.TotalHours}小时{duration.Minutes}分钟";
        }
        return $"{Math.Max(1, duration.Minutes)}分钟";
    }
}
