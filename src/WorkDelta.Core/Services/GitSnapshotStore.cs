using LibGit2Sharp;
using WorkDelta.Core.Models;
using WorkDelta.Core.Localization;

namespace WorkDelta.Core.Services;

public sealed class GitSnapshotStore(string repositoriesRoot, PathPolicy pathPolicy)
{
    private readonly string _repositoriesRoot = Path.GetFullPath(repositoriesRoot);
    private readonly PathPolicy _pathPolicy = pathPolicy;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, SemaphoreSlim> _gates = new();

    public string GetRepositoryPath(string projectId) => Path.Combine(_repositoriesRoot, projectId, "worktree");

    public async Task<SnapshotResult> CreateBaselineAsync(
        ProjectRecord project,
        AppIdentity identity,
        CancellationToken cancellationToken = default)
    {
        var gate = GetGate(project.Id);
        await gate.WaitAsync(cancellationToken);
        try
        {
            return await Task.Run(() =>
            {
                var worktree = EnsureRepository(project.Id);
                SynchronizeEntireProject(project, worktree, cancellationToken);
                return Commit(worktree, project, identity, CoreText.Choose("初始项目快照", "Initial project snapshot"));
            }, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<SnapshotResult> CreateSnapshotAsync(
        ProjectRecord project,
        IReadOnlyCollection<FileChange> changes,
        AppIdentity identity,
        string message,
        CancellationToken cancellationToken = default)
    {
        var gate = GetGate(project.Id);
        await gate.WaitAsync(cancellationToken);
        try
        {
            return await Task.Run(() =>
            {
                var worktree = EnsureRepository(project.Id);
                foreach (var change in changes)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    ApplyChange(project, worktree, change);
                }

                return Commit(worktree, project, identity, message);
            }, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<SnapshotResult> ReconcileAsync(
        ProjectRecord project,
        AppIdentity identity,
        CancellationToken cancellationToken = default)
    {
        var gate = GetGate(project.Id);
        await gate.WaitAsync(cancellationToken);
        try
        {
            return await Task.Run(() =>
            {
                var worktree = EnsureRepository(project.Id);
                SynchronizeEntireProject(project, worktree, cancellationToken);
                return Commit(worktree, project, identity, CoreText.Choose("自动校验检查点", "Automatic reconciliation checkpoint"));
            }, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<IReadOnlyList<SnapshotFileChange>> GetSnapshotChangesAsync(
        string projectId,
        string commitId,
        CancellationToken cancellationToken = default)
    {
        var gate = GetGate(projectId);
        await gate.WaitAsync(cancellationToken);
        try
        {
            return await Task.Run(() =>
            {
                using var repository = new Repository(GetRepositoryPath(projectId));
                var commit = repository.Lookup<Commit>(commitId)
                    ?? throw new InvalidOperationException(CoreText.Choose("找不到对应的历史检查点。", "The requested history checkpoint could not be found."));
                var parent = commit.Parents.FirstOrDefault();
                var changes = repository.Diff.Compare<TreeChanges>(parent?.Tree, commit.Tree);
                var result = new List<SnapshotFileChange>();
                foreach (var change in changes)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var oldPath = string.IsNullOrWhiteSpace(change.OldPath) ? change.Path : change.OldPath;
                    var oldText = parent is null ? string.Empty : ReadText(parent, oldPath);
                    var newText = ReadText(commit, change.Path);
                    var diff = BuildTextDiff(oldText, newText, change.Path);
                    result.Add(new SnapshotFileChange(
                        change.Path,
                        FriendlyStatus(change.Status),
                        diff.Added,
                        diff.Deleted,
                        diff.Text));
                }

                return (IReadOnlyList<SnapshotFileChange>)result;
            }, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task RestoreFilesAsync(
        ProjectRecord project,
        string commitId,
        IReadOnlyCollection<string> paths,
        CancellationToken cancellationToken = default)
    {
        var gate = GetGate(project.Id);
        await gate.WaitAsync(cancellationToken);
        try
        {
            await Task.Run(() =>
            {
                using var repository = new Repository(GetRepositoryPath(project.Id));
                var commit = repository.Lookup<Commit>(commitId)
                    ?? throw new InvalidOperationException(CoreText.Choose("找不到对应的历史检查点。", "The requested history checkpoint could not be found."));
                foreach (var relativePath in paths.Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    RestorePath(project.Path, commit, relativePath);
                }
            }, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task RestoreSnapshotAsync(
        ProjectRecord project,
        string commitId,
        CancellationToken cancellationToken = default)
    {
        var gate = GetGate(project.Id);
        await gate.WaitAsync(cancellationToken);
        try
        {
            await Task.Run(() =>
            {
                using var repository = new Repository(GetRepositoryPath(project.Id));
                var commit = repository.Lookup<Commit>(commitId)
                    ?? throw new InvalidOperationException(CoreText.Choose("找不到对应的历史检查点。", "The requested history checkpoint could not be found."));
                var targetPaths = EnumerateTreePaths(commit.Tree)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                foreach (var file in EnumerateFilesSafely(project.Path))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!_pathPolicy.IsTrackableFile(project.Path, file))
                    {
                        continue;
                    }

                    var relative = NormalizeRelative(Path.GetRelativePath(project.Path, file));
                    if (!targetPaths.Contains(relative))
                    {
                        File.Delete(file);
                    }
                }

                foreach (var relativePath in targetPaths)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    RestorePath(project.Path, commit, relativePath);
                }
            }, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task DeleteRepositoryAsync(string projectId, CancellationToken cancellationToken = default)
    {
        var gate = GetGate(projectId);
        await gate.WaitAsync(cancellationToken);
        try
        {
            var root = Path.Combine(_repositoriesRoot, projectId);
            if (Directory.Exists(root))
            {
                await Task.Run(() => Directory.Delete(root, true), cancellationToken);
            }
        }
        finally
        {
            gate.Release();
            _gates.TryRemove(projectId, out _);
        }
    }

    private string EnsureRepository(string projectId)
    {
        var worktree = GetRepositoryPath(projectId);
        Directory.CreateDirectory(worktree);
        if (!Repository.IsValid(worktree))
        {
            Repository.Init(worktree);
        }

        return worktree;
    }

    private void SynchronizeEntireProject(ProjectRecord project, string worktree, CancellationToken cancellationToken)
    {
        var sourceFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in EnumerateFilesSafely(project.Path))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!_pathPolicy.IsTrackableFile(project.Path, file))
            {
                continue;
            }

            var relative = NormalizeRelative(Path.GetRelativePath(project.Path, file));
            sourceFiles.Add(relative);
            CopyWithRetry(file, ResolveDestination(worktree, relative));
        }

        foreach (var mirrorFile in EnumerateFilesSafely(worktree))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsInsideGitDirectory(worktree, mirrorFile))
            {
                continue;
            }

            var relative = NormalizeRelative(Path.GetRelativePath(worktree, mirrorFile));
            if (!sourceFiles.Contains(relative))
            {
                File.Delete(mirrorFile);
            }
        }
    }

    private void ApplyChange(ProjectRecord project, string worktree, FileChange change)
    {
        var destination = ResolveDestination(worktree, change.RelativePath);
        if (change.Kind == FileChangeKind.Deleted || !File.Exists(change.FullPath))
        {
            if (File.Exists(destination))
            {
                File.Delete(destination);
            }
            else if (Directory.Exists(destination))
            {
                Directory.Delete(destination, true);
            }

            return;
        }

        if (_pathPolicy.IsTrackableFile(project.Path, change.FullPath))
        {
            CopyWithRetry(change.FullPath, destination);
        }
    }

    private static SnapshotResult Commit(
        string worktree,
        ProjectRecord project,
        AppIdentity identity,
        string message)
    {
        using var repository = new Repository(worktree);
        var changedPaths = repository.RetrieveStatus(new StatusOptions
            {
                IncludeUntracked = true,
                RecurseUntrackedDirs = true
            })
            .Where(entry => entry.State != FileStatus.Ignored)
            .Select(entry => entry.FilePath)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (changedPaths.Length == 0)
        {
            return new SnapshotResult(string.Empty, DateTimeOffset.UtcNow, 0, 0, 0, false);
        }

        Commands.Stage(repository, changedPaths);
        var treeChanges = repository.Diff.Compare<TreeChanges>(repository.Head.Tip?.Tree, DiffTargets.Index);
        var added = treeChanges.Count(change => change.Status == ChangeKind.Added);
        var deleted = treeChanges.Count(change => change.Status == ChangeKind.Deleted);
        var modified = treeChanges.Count - added - deleted;

        var now = DateTimeOffset.Now;
        var author = new Signature(identity.DisplayName, identity.GitEmail, now);
        var committer = new Signature("WorkDelta", "workdelta@local.invalid", now);
        var commitMessage = $"{message}\n\n" +
                            $"WorkDelta-Project-Id: {project.Id}\n" +
                            $"WorkDelta-User-Id: {identity.UserId}\n" +
                            $"WorkDelta-Device-Id: {identity.DeviceId}";
        var commit = repository.Commit(commitMessage, author, committer);
        return new SnapshotResult(commit.Sha, now.ToUniversalTime(), added, modified, deleted, true);
    }

    private SemaphoreSlim GetGate(string projectId) =>
        _gates.GetOrAdd(projectId, _ => new SemaphoreSlim(1, 1));

    private static string FriendlyStatus(ChangeKind status) => status switch
    {
        ChangeKind.Added => CoreText.Choose("新增", "Added"),
        ChangeKind.Deleted => CoreText.Choose("删除", "Deleted"),
        ChangeKind.Renamed => CoreText.Choose("重命名", "Renamed"),
        ChangeKind.Copied => CoreText.Choose("复制", "Copied"),
        ChangeKind.Modified => CoreText.Choose("修改", "Modified"),
        _ => CoreText.Choose("变化", "Changed")
    };

    private static string ReadText(Commit commit, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || commit.Tree[path]?.Target is not Blob blob)
        {
            return string.Empty;
        }

        using var stream = blob.GetContentStream();
        using var reader = new StreamReader(stream, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    private static (string Text, int Added, int Deleted) BuildTextDiff(
        string oldText,
        string newText,
        string path)
    {
        var oldLines = NormalizeLines(oldText);
        var newLines = NormalizeLines(newText);
        if ((long)oldLines.Length * newLines.Length > 1_500_000)
        {
            var changed = Math.Max(oldLines.Length, newLines.Length);
            return (CoreText.Format("--- 旧版本/{0}\n+++ 新版本/{0}\n@@ 文件较大，共约 {1} 行；恢复功能仍可正常使用。", "--- old/{0}\n+++ new/{0}\n@@ Large file with approximately {1} changed lines; restore remains available.", path, changed),
                Math.Max(0, newLines.Length - oldLines.Length),
                Math.Max(0, oldLines.Length - newLines.Length));
        }

        var lengths = new int[oldLines.Length + 1, newLines.Length + 1];
        for (var oldIndex = oldLines.Length - 1; oldIndex >= 0; oldIndex--)
        {
            for (var newIndex = newLines.Length - 1; newIndex >= 0; newIndex--)
            {
                lengths[oldIndex, newIndex] = oldLines[oldIndex] == newLines[newIndex]
                    ? lengths[oldIndex + 1, newIndex + 1] + 1
                    : Math.Max(lengths[oldIndex + 1, newIndex], lengths[oldIndex, newIndex + 1]);
            }
        }

        var builder = new System.Text.StringBuilder()
            .AppendLine($"--- {CoreText.Choose("旧版本", "old")}/{path}")
            .AppendLine($"+++ {CoreText.Choose("新版本", "new")}/{path}");
        var i = 0;
        var j = 0;
        var added = 0;
        var deleted = 0;
        while (i < oldLines.Length && j < newLines.Length)
        {
            if (oldLines[i] == newLines[j])
            {
                builder.Append("  ").AppendLine(oldLines[i]);
                i++;
                j++;
            }
            else if (lengths[i + 1, j] >= lengths[i, j + 1])
            {
                builder.Append("- ").AppendLine(oldLines[i++]);
                deleted++;
            }
            else
            {
                builder.Append("+ ").AppendLine(newLines[j++]);
                added++;
            }
        }

        while (i < oldLines.Length)
        {
            builder.Append("- ").AppendLine(oldLines[i++]);
            deleted++;
        }
        while (j < newLines.Length)
        {
            builder.Append("+ ").AppendLine(newLines[j++]);
            added++;
        }

        return (builder.ToString(), added, deleted);
    }

    private static string[] NormalizeLines(string text) =>
        text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

    private static IEnumerable<string> EnumerateTreePaths(Tree tree, string prefix = "")
    {
        foreach (var entry in tree)
        {
            var path = string.IsNullOrEmpty(prefix) ? entry.Name : $"{prefix}/{entry.Name}";
            if (entry.Target is Tree child)
            {
                foreach (var childPath in EnumerateTreePaths(child, path))
                {
                    yield return childPath;
                }
            }
            else if (entry.Target is Blob)
            {
                yield return path;
            }
        }
    }

    private static void RestorePath(string projectRoot, Commit commit, string relativePath)
    {
        var destination = ResolveDestination(projectRoot, relativePath);
        if (commit.Tree[relativePath]?.Target is not Blob blob)
        {
            if (File.Exists(destination))
            {
                File.Delete(destination);
            }
            return;
        }

        var directory = Path.GetDirectoryName(destination);
        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
        }
        using var input = blob.GetContentStream();
        using var output = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.Read);
        input.CopyTo(output);
    }

    private static string ResolveDestination(string worktree, string relativePath)
    {
        var normalized = relativePath.Replace('/', Path.DirectorySeparatorChar);
        var destination = Path.GetFullPath(Path.Combine(worktree, normalized));
        var rootWithSeparator = Path.TrimEndingDirectorySeparator(Path.GetFullPath(worktree)) + Path.DirectorySeparatorChar;
        if (!destination.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(CoreText.Choose("检测到无效的项目文件路径。", "An invalid project file path was detected."));
        }

        return destination;
    }

    private static void CopyWithRetry(string source, string destination)
    {
        var directory = Path.GetDirectoryName(destination);
        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
        }

        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var output = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.Read);
                input.CopyTo(output);
                return;
            }
            catch (IOException) when (attempt < 3)
            {
                Thread.Sleep(attempt * 100);
            }
        }
    }

    private static IEnumerable<string> EnumerateFilesSafely(string root)
    {
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var directory = pending.Pop();
            if (Path.GetFileName(directory).Equals(".git", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string[] files;
            string[] directories;
            try
            {
                files = Directory.GetFiles(directory);
                directories = Directory.GetDirectories(directory);
            }
            catch (IOException)
            {
                continue;
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var file in files)
            {
                yield return file;
            }

            foreach (var child in directories)
            {
                pending.Push(child);
            }
        }
    }

    private static bool IsInsideGitDirectory(string worktree, string file)
    {
        var gitDirectory = Path.Combine(Path.GetFullPath(worktree), ".git") + Path.DirectorySeparatorChar;
        return Path.GetFullPath(file).StartsWith(gitDirectory, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeRelative(string relativePath) => relativePath.Replace('\\', '/');
}
