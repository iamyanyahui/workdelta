using WorkDelta.Core.Services;

namespace WorkDelta.Core.Tests;

public sealed class BackupServiceTests
{
    [Fact]
    public async Task Backup_RoundTripsDatabaseAndProjects()
    {
        var root = NewTemporaryDirectory();
        var dataRoot = Directory.CreateDirectory(Path.Combine(root, "data")).FullName;
        var projectFolder = Directory.CreateDirectory(Path.Combine(root, "project")).FullName;
        var store = new WorkDeltaStore(Path.Combine(dataRoot, "workdelta.db"));
        await store.InitializeAsync();
        await store.AddProjectAsync(projectFolder);
        var archive = Path.Combine(root, "backup.workdelta");
        await new BackupService(dataRoot, store).ExportAsync(archive);

        var restoredRoot = Path.Combine(root, "restored");
        BackupService.RestoreBeforeStartup(restoredRoot, archive);
        var restored = new WorkDeltaStore(Path.Combine(restoredRoot, "workdelta.db"));
        await restored.InitializeAsync();

        Assert.Single(await restored.GetProjectsAsync());
    }

    private static string NewTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "WorkDelta.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
