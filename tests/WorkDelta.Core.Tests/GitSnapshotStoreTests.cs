using WorkDelta.Core.Models;
using WorkDelta.Core.Services;

namespace WorkDelta.Core.Tests;

public sealed class GitSnapshotStoreTests
{
    [Fact]
    public async Task Snapshots_TextChanges_WithoutTouchingSourceFolder()
    {
        var root = NewTemporaryDirectory();
        var projectFolder = Directory.CreateDirectory(Path.Combine(root, "source")).FullName;
        var sourceFile = Path.Combine(projectFolder, "notes.txt");
        await File.WriteAllTextAsync(sourceFile, "第一版");
        var project = new ProjectRecord("project1", "source", projectFolder, DateTimeOffset.UtcNow, null, null, false);
        var identity = new AppIdentity("1234567890abcdef", "测试用户", "device1", "测试设备");
        var store = new GitSnapshotStore(Path.Combine(root, "repositories"), new PathPolicy());

        var baseline = await store.CreateBaselineAsync(project, identity);
        await File.WriteAllTextAsync(sourceFile, "第二版");
        var snapshot = await store.CreateSnapshotAsync(
            project,
            [new FileChange(sourceFile, "notes.txt", FileChangeKind.Modified)],
            identity,
            "自动检查点");

        Assert.True(baseline.HasChanges);
        Assert.True(snapshot.HasChanges);
        Assert.False(Directory.Exists(Path.Combine(projectFolder, ".git")));
        Assert.True(Directory.Exists(Path.Combine(store.GetRepositoryPath(project.Id), ".git")));
    }

    private static string NewTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "WorkDelta.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
