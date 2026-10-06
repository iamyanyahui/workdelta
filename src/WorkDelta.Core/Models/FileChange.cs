namespace WorkDelta.Core.Models;
public enum FileChangeKind { Created, Modified, Deleted, Renamed }
public sealed record FileChange(string FullPath, string RelativePath, FileChangeKind Kind);
