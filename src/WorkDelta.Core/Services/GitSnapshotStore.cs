using LibGit2Sharp;
using WorkDelta.Core.Models;

namespace WorkDelta.Core.Services;

public sealed class GitSnapshotStore(string repositoriesRoot, PathPolicy pathPolicy)
{
    private readonly string _repositoriesRoot = Path.GetFullPath(repositoriesRoot);
    private readonly PathPolicy _pathPolicy = pathPolicy;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public string GetRepositoryPath(string projectId) => Path.Combine(_repositoriesRoot, projectId, "worktree");

    public async Task<SnapshotResult> CreateBaselineAsync(
        ProjectRecord project,
        AppIdentity identity,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return await Task.Run(() =>
            {
                var worktree = EnsureRepository(project.Id);
                SynchronizeEntireProject(project, worktree, cancellationToken);
                return Commit(worktree, project, identity, "初始项目快照");
            }, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<SnapshotResult> CreateSnapshotAsync(
        ProjectRecord project,
        IReadOnlyCollection<FileChange> changes,
        AppIdentity identity,
        string message,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
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
            _gate.Release();
        }
    }

    public async Task<SnapshotResult> ReconcileAsync(
        ProjectRecord project,
        AppIdentity identity,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return await Task.Run(() =>
            {
                var worktree = EnsureRepository(project.Id);
                SynchronizeEntireProject(project, worktree, cancellationToken);
                return Commit(worktree, project, identity, "自动校验检查点");
            }, cancellationToken);
        }
        finally
        {
            _gate.Release();
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

    private static string ResolveDestination(string worktree, string relativePath)
    {
        var normalized = relativePath.Replace('/', Path.DirectorySeparatorChar);
        var destination = Path.GetFullPath(Path.Combine(worktree, normalized));
        var rootWithSeparator = Path.TrimEndingDirectorySeparator(Path.GetFullPath(worktree)) + Path.DirectorySeparatorChar;
        if (!destination.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("检测到无效的项目文件路径。");
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
