namespace WorkDelta.Core.Models;
public sealed record ProjectRecord(string Id, string Name, string Path, DateTimeOffset CreatedAt, DateTimeOffset? LastActivityAt, DateTimeOffset? LastSnapshotAt, bool IsPaused);
