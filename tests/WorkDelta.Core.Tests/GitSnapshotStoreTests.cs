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

    [Fact]
    public async Task History_ShowsTextDiff_AndRestoresSelectedFile()
    {
        var root = NewTemporaryDirectory();
        var projectFolder = Directory.CreateDirectory(Path.Combine(root, "source")).FullName;
        var sourceFile = Path.Combine(projectFolder, "notes.txt");
        await File.WriteAllTextAsync(sourceFile, "第一行\n旧内容");
        var project = new ProjectRecord("project2", "source", projectFolder, DateTimeOffset.UtcNow, null, null, false);
        var identity = new AppIdentity("1234567890abcdef", "测试用户", "device1", "测试设备");
        var store = new GitSnapshotStore(Path.Combine(root, "repositories"), new PathPolicy());
        var baseline = await store.CreateBaselineAsync(project, identity);
        var baselineChanges = await store.GetSnapshotChangesAsync(project.Id, baseline.CommitId);
        Assert.Single(baselineChanges);

        await File.WriteAllTextAsync(sourceFile, "第一行\n新内容\n第三行");
        var latest = await store.CreateSnapshotAsync(
            project,
            [new FileChange(sourceFile, "notes.txt", FileChangeKind.Modified)],
            identity,
            "第二版");

        var changes = await store.GetSnapshotChangesAsync(project.Id, latest.CommitId);
        var change = Assert.Single(changes);
        Assert.Contains("+ 新内容", change.Diff);
        Assert.Contains("- 旧内容", change.Diff);

        await store.RestoreFilesAsync(project, baseline.CommitId, ["notes.txt"]);
        Assert.Equal("第一行\n旧内容", await File.ReadAllTextAsync(sourceFile));
    }

    private static string NewTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "WorkDelta.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
