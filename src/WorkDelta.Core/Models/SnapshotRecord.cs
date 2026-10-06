namespace WorkDelta.Core.Models;
public sealed record SnapshotRecord(string Id, string ProjectId, string CommitId, DateTimeOffset CreatedAt, string Kind, int Added, int Modified, int Deleted);
public sealed record SnapshotResult(string CommitId, DateTimeOffset CreatedAt, int Added, int Modified, int Deleted, bool HasChanges);
