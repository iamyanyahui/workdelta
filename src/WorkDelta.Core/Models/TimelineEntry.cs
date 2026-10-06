namespace WorkDelta.Core.Models;
public sealed record TimelineEntry(string SessionId, DateTimeOffset StartedAt, DateTimeOffset LastActivityAt, int ChangeCount, int FileCount, IReadOnlyList<string> Files)
{
    public TimeSpan ActiveDuration { get { var value = LastActivityAt - StartedAt; return value < TimeSpan.FromMinutes(1) ? TimeSpan.FromMinutes(1) : value; } }
}
