using WorkDelta.Core.Models;
using WorkDelta.Core.Services;

namespace WorkDelta.Core.Tests;

public sealed class WorkDeltaStoreTests
{
    [Fact]
    public async Task RecordChanges_GroupsNearbyChangesIntoOneSession()
    {
        var root = NewTemporaryDirectory();
        var projectFolder = Directory.CreateDirectory(Path.Combine(root, "project")).FullName;
        var store = new WorkDeltaStore(Path.Combine(root, "data.db"));
        await store.InitializeAsync();
        var project = await store.AddProjectAsync(projectFolder);
        var start = DateTimeOffset.UtcNow.AddMinutes(-5);

        await store.RecordChangesAsync(project,
            [new FileChange(Path.Combine(projectFolder, "a.txt"), "a.txt", FileChangeKind.Modified)], start);
        await store.RecordChangesAsync(project,
            [new FileChange(Path.Combine(projectFolder, "b.txt"), "b.txt", FileChangeKind.Created)], start.AddMinutes(5));

        var timeline = await store.GetTimelineAsync(
            project.Id,
            DateOnly.FromDateTime(DateTime.Now),
            TimeZoneInfo.Local);

        var session = Assert.Single(timeline);
        Assert.Equal(2, session.ChangeCount);
        Assert.Equal(2, session.FileCount);
    }

    [Fact]
    public async Task RecordChanges_SplitsSessionAfterFifteenMinuteGap()
    {
        var root = NewTemporaryDirectory();
        var projectFolder = Directory.CreateDirectory(Path.Combine(root, "project")).FullName;
        var store = new WorkDeltaStore(Path.Combine(root, "data.db"));
        await store.InitializeAsync();
        var project = await store.AddProjectAsync(projectFolder);
        var start = DateTimeOffset.UtcNow.AddHours(-1);

        await store.RecordChangesAsync(project,
            [new FileChange(Path.Combine(projectFolder, "a.txt"), "a.txt", FileChangeKind.Modified)], start);
        await store.RecordChangesAsync(project,
            [new FileChange(Path.Combine(projectFolder, "a.txt"), "a.txt", FileChangeKind.Modified)], start.AddMinutes(16));

        var localDate = DateOnly.FromDateTime(start.ToLocalTime().DateTime);
        var timeline = await store.GetTimelineAsync(project.Id, localDate, TimeZoneInfo.Local);

        Assert.Equal(2, timeline.Count);
    }

    [Fact]
    public async Task Reports_AggregateProjectsAcrossDateRange()
    {
        var root = NewTemporaryDirectory();
        var projectFolder = Directory.CreateDirectory(Path.Combine(root, "project")).FullName;
        var store = new WorkDeltaStore(Path.Combine(root, "data.db"));
        await store.InitializeAsync();
        var project = await store.AddProjectAsync(projectFolder);
        var now = DateTimeOffset.UtcNow;
        await store.RecordChangesAsync(project,
            [new FileChange(Path.Combine(projectFolder, "a.txt"), "a.txt", FileChangeKind.Modified)], now);

        var today = DateOnly.FromDateTime(DateTime.Today);
        var reports = await store.GetActivityReportsAsync(today, today, TimeZoneInfo.Local);
        var report = Assert.Single(reports);
        Assert.Equal(project.Name, report.ProjectName);
        Assert.Equal(1, report.FileCount);
        Assert.Equal(TimeSpan.FromMinutes(1), report.ActiveDuration);
    }

    [Fact]
    public async Task ProjectSettings_CanRenameMoveAndSaveIgnoreRules()
    {
        var root = NewTemporaryDirectory();
        var first = Directory.CreateDirectory(Path.Combine(root, "first")).FullName;
        var second = Directory.CreateDirectory(Path.Combine(root, "second")).FullName;
        var store = new WorkDeltaStore(Path.Combine(root, "data.db"));
        await store.InitializeAsync();
        var project = await store.AddProjectAsync(first);

        var updated = await store.UpdateProjectAsync(project.Id, "新名称", second);
        await store.SetIgnorePatternsAsync(project.Id, "generated/*");

        Assert.Equal("新名称", updated.Name);
        Assert.Equal(second, updated.Path);
        Assert.Equal("generated/*", await store.GetIgnorePatternsAsync(project.Id));
    }

    private static string NewTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "WorkDelta.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
