using System.Collections.Concurrent;
using WorkDelta.Core.Models;

namespace WorkDelta.Core.Services;

internal sealed class ProjectMonitor : IDisposable
{
    private readonly ProjectRecord _project;
    private readonly PathPolicy _pathPolicy;
    private readonly Func<IReadOnlyList<FileChange>, Task> _onBatch;
    private readonly Func<Task> _onWatcherError;
    private readonly FileSystemWatcher _watcher;
    private readonly ConcurrentDictionary<string, FileChangeKind> _pending = new(StringComparer.OrdinalIgnoreCase);
    private readonly Timer _timer;
    private readonly SemaphoreSlim _flushGate = new(1, 1);
    private bool _disposed;

    public ProjectMonitor(
        ProjectRecord project,
        PathPolicy pathPolicy,
        Func<IReadOnlyList<FileChange>, Task> onBatch,
        Func<Task> onWatcherError)
    {
        _project = project;
        _pathPolicy = pathPolicy;
        _onBatch = onBatch;
        _onWatcherError = onWatcherError;
        _timer = new Timer(_ => _ = FlushAsync(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _watcher = new FileSystemWatcher(project.Path)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName |
                           NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime,
            InternalBufferSize = 64 * 1024,
            EnableRaisingEvents = false
        };
        _watcher.Created += (_, args) => Enqueue(args.FullPath, FileChangeKind.Created);
        _watcher.Changed += (_, args) => Enqueue(args.FullPath, FileChangeKind.Modified);
        _watcher.Deleted += (_, args) => Enqueue(args.FullPath, FileChangeKind.Deleted);
        _watcher.Renamed += (_, args) =>
        {
            Enqueue(args.OldFullPath, FileChangeKind.Deleted);
            Enqueue(args.FullPath, FileChangeKind.Renamed);
        };
        _watcher.Error += (_, _) => _ = _onWatcherError();
    }

    public void Start() => _watcher.EnableRaisingEvents = true;

    public async Task FlushAsync()
    {
        if (_disposed || !await _flushGate.WaitAsync(0))
        {
            return;
        }

        try
        {
            var snapshot = _pending.ToArray();
            if (snapshot.Length == 0)
            {
                return;
            }

            foreach (var item in snapshot)
            {
                _pending.TryRemove(item.Key, out _);
            }

            var changes = snapshot
                .Where(item => !_pathPolicy.ShouldIgnore(_project.Path, item.Key))
                .Select(item => new FileChange(
                    item.Key,
                    Path.GetRelativePath(_project.Path, item.Key).Replace('\\', '/'),
                    item.Value))
                .Where(change => !change.RelativePath.StartsWith("..", StringComparison.Ordinal))
                .ToArray();
            if (changes.Length > 0)
            {
                await _onBatch(changes);
            }
        }
        finally
        {
            _flushGate.Release();
        }
    }

    private void Enqueue(string fullPath, FileChangeKind kind)
    {
        if (_disposed || _pathPolicy.ShouldIgnore(_project.Path, fullPath))
        {
            return;
        }

        _pending.AddOrUpdate(fullPath, kind, (_, existing) => Merge(existing, kind));
        _timer.Change(TimeSpan.FromSeconds(2), Timeout.InfiniteTimeSpan);
    }

    private static FileChangeKind Merge(FileChangeKind existing, FileChangeKind incoming)
    {
        if (incoming == FileChangeKind.Deleted)
        {
            return FileChangeKind.Deleted;
        }

        if (existing == FileChangeKind.Created)
        {
            return FileChangeKind.Created;
        }

        return incoming;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _watcher.EnableRaisingEvents = false;
        _watcher.Dispose();
        _timer.Dispose();
        _flushGate.Dispose();
    }
}
