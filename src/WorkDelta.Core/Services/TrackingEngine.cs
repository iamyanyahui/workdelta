using System.Collections.Concurrent;
using System.Security.Cryptography;
using WorkDelta.Core.Models;

namespace WorkDelta.Core.Services;

public sealed class TrackingEngine(
    WorkDeltaStore store,
    GitSnapshotStore snapshotStore,
    PathPolicy pathPolicy,
    AppIdentity identity) : IAsyncDisposable
{
    private static readonly TimeSpan SnapshotQuietPeriod = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan MaximumSnapshotInterval = TimeSpan.FromMinutes(10);
    private readonly WorkDeltaStore _store = store;
    private readonly GitSnapshotStore _snapshotStore = snapshotStore;
    private readonly PathPolicy _pathPolicy = pathPolicy;
    private readonly AppIdentity _identity = identity;
    private readonly Dictionary<string, ProjectMonitor> _monitors = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, FileChange>> _snapshotChanges = new();
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _snapshotTimers = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastSnapshotAt = new();
    private readonly ConcurrentDictionary<string, string> _contentHashes = new(StringComparer.OrdinalIgnoreCase);
    private readonly CancellationTokenSource _shutdown = new();

    public event EventHandler<ActivityRecordedEventArgs>? ActivityRecorded;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var projects = await _store.GetProjectsAsync(cancellationToken);
        foreach (var project in projects)
        {
            _pathPolicy.SetCustomRules(project.Path, await _store.GetIgnorePatternsAsync(project.Id, cancellationToken));
            if (project.IsPaused || !Directory.Exists(project.Path))
            {
                continue;
            }
            if (project.LastSnapshotAt is not null)
            {
                _lastSnapshotAt[project.Id] = project.LastSnapshotAt.Value;
            }
            StartMonitor(project);
        }
    }

    public async Task<ProjectRecord> AddProjectAsync(string path, CancellationToken cancellationToken = default)
    {
        var project = await _store.AddProjectAsync(path, cancellationToken);
        _pathPolicy.SetCustomRules(project.Path, await _store.GetIgnorePatternsAsync(project.Id, cancellationToken));
        var baseline = await _snapshotStore.CreateBaselineAsync(project, _identity, cancellationToken);
        await _store.AddSnapshotAsync(project, baseline, "baseline", cancellationToken);
        if (baseline.HasChanges)
        {
            _lastSnapshotAt[project.Id] = baseline.CreatedAt;
        }
        StartMonitor(project);
        return (await _store.GetProjectAsync(project.Id, cancellationToken)) ?? project;
    }

    public async Task SetPausedAsync(ProjectRecord project, bool paused, CancellationToken cancellationToken = default)
    {
        await _store.SetProjectPausedAsync(project.Id, paused, cancellationToken);
        if (paused)
        {
            if (_monitors.Remove(project.Id, out var monitor))
            {
                await monitor.FlushAsync();
                monitor.Dispose();
            }
        }
        else
        {
            var updated = await _store.GetProjectAsync(project.Id, cancellationToken);
            if (updated is not null && Directory.Exists(updated.Path))
            {
                StartMonitor(updated);
            }
        }
    }

    public async Task<SnapshotResult> CreateSnapshotNowAsync(ProjectRecord project, CancellationToken cancellationToken = default)
    {
        if (_snapshotChanges.TryRemove(project.Id, out var changes) && changes.Count > 0)
        {
            CancelTimer(project.Id);
            var result = await _snapshotStore.CreateSnapshotAsync(
                project,
                changes.Values.ToArray(),
                _identity,
                "手动检查点",
                cancellationToken);
            await _store.AddSnapshotAsync(project, result, "manual", cancellationToken);
            if (result.HasChanges)
            {
                _lastSnapshotAt[project.Id] = result.CreatedAt;
            }
            return result;
        }

        var reconciled = await _snapshotStore.ReconcileAsync(project, _identity, cancellationToken);
        await _store.AddSnapshotAsync(project, reconciled, "manual", cancellationToken);
        if (reconciled.HasChanges)
        {
            _lastSnapshotAt[project.Id] = reconciled.CreatedAt;
        }
        return reconciled;
    }

    public Task<IReadOnlyList<SnapshotFileChange>> GetSnapshotChangesAsync(
        ProjectRecord project,
        string commitId,
        CancellationToken cancellationToken = default) =>
        _snapshotStore.GetSnapshotChangesAsync(project.Id, commitId, cancellationToken);

    public async Task RestoreFilesAsync(
        ProjectRecord project,
        string commitId,
        IReadOnlyCollection<string> paths,
        CancellationToken cancellationToken = default)
    {
        await _snapshotStore.RestoreFilesAsync(project, commitId, paths, cancellationToken);
        await SaveRestoreCheckpointAsync(project, cancellationToken);
    }

    public async Task RestoreSnapshotAsync(
        ProjectRecord project,
        string commitId,
        CancellationToken cancellationToken = default)
    {
        await _snapshotStore.RestoreSnapshotAsync(project, commitId, cancellationToken);
        await SaveRestoreCheckpointAsync(project, cancellationToken);
    }

    private async Task SaveRestoreCheckpointAsync(ProjectRecord project, CancellationToken cancellationToken)
    {
        var result = await _snapshotStore.ReconcileAsync(project, _identity, cancellationToken);
        await _store.AddSnapshotAsync(project, result, "restore", cancellationToken);
        if (result.HasChanges)
        {
            _lastSnapshotAt[project.Id] = result.CreatedAt;
        }
    }

    public async Task<ProjectRecord> UpdateProjectAsync(
        ProjectRecord project,
        string name,
        string path,
        string ignorePatterns,
        CancellationToken cancellationToken = default)
    {
        await StopMonitorAsync(project.Id);
        _pathPolicy.RemoveCustomRules(project.Path);
        var updated = await _store.UpdateProjectAsync(project.Id, name, path, cancellationToken);
        await _store.SetIgnorePatternsAsync(project.Id, ignorePatterns, cancellationToken);
        _pathPolicy.SetCustomRules(updated.Path, ignorePatterns);
        if (!updated.IsPaused)
        {
            StartMonitor(updated);
        }
        await ReconcileAsync(updated);
        return (await _store.GetProjectAsync(updated.Id, cancellationToken)) ?? updated;
    }

    public async Task DeleteProjectAsync(ProjectRecord project, CancellationToken cancellationToken = default)
    {
        await StopMonitorAsync(project.Id);
        CancelTimer(project.Id);
        _snapshotChanges.TryRemove(project.Id, out _);
        _pathPolicy.RemoveCustomRules(project.Path);
        foreach (var key in _contentHashes.Keys.Where(key => key.StartsWith(project.Id + "|", StringComparison.Ordinal)).ToArray())
        {
            _contentHashes.TryRemove(key, out _);
        }
        await _snapshotStore.DeleteRepositoryAsync(project.Id, cancellationToken);
        await _store.DeleteProjectAsync(project.Id, cancellationToken);
    }

    private void StartMonitor(ProjectRecord project)
    {
        if (_monitors.ContainsKey(project.Id))
        {
            return;
        }

        var monitor = new ProjectMonitor(
            project,
            _pathPolicy,
            changes => HandleBatchAsync(project, changes),
            () => ReconcileAsync(project));
        _monitors.Add(project.Id, monitor);
        monitor.Start();
    }

    private async Task HandleBatchAsync(ProjectRecord project, IReadOnlyList<FileChange> changes)
    {
        try
        {
            var meaningfulChanges = await FilterMeaningfulChangesAsync(project, changes, _shutdown.Token);
            if (meaningfulChanges.Count == 0)
            {
                return;
            }
            await _store.RecordChangesAsync(project, meaningfulChanges, DateTimeOffset.UtcNow, _shutdown.Token);
            var pending = _snapshotChanges.GetOrAdd(
                project.Id,
                _ => new ConcurrentDictionary<string, FileChange>(StringComparer.OrdinalIgnoreCase));
            foreach (var change in meaningfulChanges)
            {
                pending[change.RelativePath] = change;
            }

            ScheduleSnapshot(project);
            ActivityRecorded?.Invoke(this, new ActivityRecordedEventArgs(project, meaningfulChanges));
        }
        catch (OperationCanceledException) when (_shutdown.IsCancellationRequested)
        {
        }
    }

    private async Task<IReadOnlyList<FileChange>> FilterMeaningfulChangesAsync(
        ProjectRecord project,
        IReadOnlyList<FileChange> changes,
        CancellationToken cancellationToken)
    {
        var meaningful = new List<FileChange>(changes.Count);
        foreach (var change in changes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var key = project.Id + "|" + change.RelativePath;
            if (change.Kind == FileChangeKind.Deleted || !File.Exists(change.FullPath))
            {
                _contentHashes.TryRemove(key, out _);
                meaningful.Add(change);
                continue;
            }

            if (!_pathPolicy.IsTrackableFile(project.Path, change.FullPath))
            {
                continue;
            }

            string? hash = null;
            try
            {
                hash = await ComputeHashAsync(change.FullPath, cancellationToken);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }

            if (hash is not null &&
                change.Kind == FileChangeKind.Modified &&
                _contentHashes.TryGetValue(key, out var previous) &&
                previous == hash)
            {
                continue;
            }

            if (hash is not null)
            {
                _contentHashes[key] = hash;
            }
            meaningful.Add(change);
        }
        return meaningful;
    }

    private static async Task<string> ComputeHashAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash);
    }

    private async Task StopMonitorAsync(string projectId)
    {
        if (_monitors.Remove(projectId, out var monitor))
        {
            await monitor.FlushAsync();
            monitor.Dispose();
        }
    }

    private void ScheduleSnapshot(ProjectRecord project)
    {
        CancelTimer(project.Id);
        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_shutdown.Token);
        _snapshotTimers[project.Id] = cancellation;
        var delay = !_lastSnapshotAt.TryGetValue(project.Id, out var lastSnapshot) ||
                    DateTimeOffset.UtcNow - lastSnapshot >= MaximumSnapshotInterval
            ? TimeSpan.FromSeconds(5)
            : SnapshotQuietPeriod;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(delay, cancellation.Token);
                await SavePendingSnapshotAsync(project, "automatic", cancellation.Token);
            }
            catch (OperationCanceledException)
            {
            }
        }, cancellation.Token);
    }

    private async Task SavePendingSnapshotAsync(ProjectRecord project, string kind, CancellationToken cancellationToken)
    {
        if (!_snapshotChanges.TryRemove(project.Id, out var changes) || changes.Count == 0)
        {
            return;
        }

        var message = $"自动检查点：{changes.Count} 个文件发生变化";
        var result = await _snapshotStore.CreateSnapshotAsync(
            project,
            changes.Values.ToArray(),
            _identity,
            message,
            cancellationToken);
        await _store.AddSnapshotAsync(project, result, kind, cancellationToken);
        if (result.HasChanges)
        {
            _lastSnapshotAt[project.Id] = result.CreatedAt;
        }
    }

    private async Task ReconcileAsync(ProjectRecord project)
    {
        try
        {
            var result = await _snapshotStore.ReconcileAsync(project, _identity, _shutdown.Token);
            await _store.AddSnapshotAsync(project, result, "reconcile", _shutdown.Token);
            if (result.HasChanges)
            {
                _lastSnapshotAt[project.Id] = result.CreatedAt;
            }
        }
        catch (OperationCanceledException) when (_shutdown.IsCancellationRequested)
        {
        }
    }

    private void CancelTimer(string projectId)
    {
        if (_snapshotTimers.TryRemove(projectId, out var previous))
        {
            previous.Cancel();
            previous.Dispose();
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var monitor in _monitors.Values)
        {
            await monitor.FlushAsync();
            monitor.Dispose();
        }
        _monitors.Clear();

        foreach (var projectId in _snapshotChanges.Keys.ToArray())
        {
            var project = await _store.GetProjectAsync(projectId);
            if (project is not null)
            {
                try
                {
                    await SavePendingSnapshotAsync(project, "shutdown", CancellationToken.None);
                }
                catch (IOException)
                {
                }
            }
        }

        _shutdown.Cancel();
        foreach (var timer in _snapshotTimers.Values)
        {
            timer.Cancel();
            timer.Dispose();
        }
        _snapshotTimers.Clear();
        _shutdown.Dispose();
    }
}
