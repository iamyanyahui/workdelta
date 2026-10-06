namespace WorkDelta.Core.Models;
public sealed class ActivityRecordedEventArgs(ProjectRecord project, IReadOnlyList<FileChange> changes) : EventArgs
{
    public ProjectRecord Project { get; } = project;
    public IReadOnlyList<FileChange> Changes { get; } = changes;
}
