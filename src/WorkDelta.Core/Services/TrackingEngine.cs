using System.Collections.Concurrent;
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
    private readonly CancellationTokenSource _shutdown = new();

    public event EventHandler<ActivityRecordedEventArgs>? ActivityRecorded;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var projects = await _store.GetProjectsAsync(cancellationToken);
        foreach (var project in projects.Where(item => !item.IsPaused && Directory.Exists(item.Path)))
        {
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
            await _store.RecordChangesAsync(project, changes, DateTimeOffset.UtcNow, _shutdown.Token);
            var pending = _snapshotChanges.GetOrAdd(
                project.Id,
                _ => new ConcurrentDictionary<string, FileChange>(StringComparer.OrdinalIgnoreCase));
            foreach (var change in changes)
            {
                pending[change.RelativePath] = change;
            }

            ScheduleSnapshot(project);
            ActivityRecorded?.Invoke(this, new ActivityRecordedEventArgs(project, changes));
        }
        catch (OperationCanceledException) when (_shutdown.IsCancellationRequested)
        {
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
