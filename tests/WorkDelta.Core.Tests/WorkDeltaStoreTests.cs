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

    private static string NewTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "WorkDelta.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
