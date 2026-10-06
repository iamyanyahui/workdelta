using System.IO.Compression;
using System.Text.Json;

namespace WorkDelta.Core.Services;

public sealed class BackupService(string dataRoot, WorkDeltaStore store)
{
    private readonly string _dataRoot = Path.GetFullPath(dataRoot);
    private readonly WorkDeltaStore _store = store;

    public async Task ExportAsync(string destinationPath, CancellationToken cancellationToken = default)
    {
        var temporary = Path.Combine(Path.GetTempPath(), "WorkDelta.Backup", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporary);
        try
        {
            await _store.CreateDatabaseBackupAsync(Path.Combine(temporary, "workdelta.db"), cancellationToken);
            var repositories = Path.Combine(_dataRoot, "Repositories");
            if (Directory.Exists(repositories))
            {
                CopyDirectory(repositories, Path.Combine(temporary, "Repositories"), cancellationToken);
            }

            var manifest = new
            {
                formatVersion = 1,
                createdAtUtc = DateTimeOffset.UtcNow,
                product = "WorkDelta"
            };
            await File.WriteAllTextAsync(
                Path.Combine(temporary, "manifest.json"),
                JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }),
                cancellationToken);

            if (File.Exists(destinationPath))
            {
                File.Delete(destinationPath);
            }
            ZipFile.CreateFromDirectory(temporary, destinationPath, CompressionLevel.Optimal, false);
        }
        finally
        {
            if (Directory.Exists(temporary))
            {
                Directory.Delete(temporary, true);
            }
        }
    }

    public static void RestoreBeforeStartup(string dataRoot, string archivePath)
    {
        if (!File.Exists(archivePath))
        {
            throw new FileNotFoundException("找不到工迹备份文件。", archivePath);
        }

        var fullDataRoot = Path.GetFullPath(dataRoot);
        var temporary = Path.Combine(Path.GetTempPath(), "WorkDelta.Restore", Guid.NewGuid().ToString("N"));
        var rollback = Path.Combine(Path.GetTempPath(), "WorkDelta.Rollback", Guid.NewGuid().ToString("N"));
        var rollbackReady = false;
        Directory.CreateDirectory(temporary);
        Directory.CreateDirectory(rollback);
        try
        {
            ZipFile.ExtractToDirectory(archivePath, temporary, overwriteFiles: true);
            if (!File.Exists(Path.Combine(temporary, "manifest.json")) ||
                !File.Exists(Path.Combine(temporary, "workdelta.db")))
            {
                throw new InvalidDataException("这不是有效的工迹备份文件。");
            }

            Directory.CreateDirectory(fullDataRoot);
            CopyCurrentForRollback(fullDataRoot, rollback);
            rollbackReady = true;
            ReplaceData(fullDataRoot, temporary);
        }
        catch
        {
            try
            {
                if (rollbackReady)
                {
                    ReplaceData(fullDataRoot, rollback);
                }
            }
            catch
            {
            }
            throw;
        }
        finally
        {
            TryDeleteDirectory(temporary);
            TryDeleteDirectory(rollback);
        }
    }

    private static void ReplaceData(string dataRoot, string source)
    {
        foreach (var fileName in new[] { "workdelta.db", "workdelta.db-wal", "workdelta.db-shm" })
        {
            var target = Path.Combine(dataRoot, fileName);
            if (File.Exists(target))
            {
                File.Delete(target);
            }
        }

        var targetRepositories = Path.Combine(dataRoot, "Repositories");
        if (Directory.Exists(targetRepositories))
        {
            Directory.Delete(targetRepositories, true);
        }

        var sourceDatabase = Path.Combine(source, "workdelta.db");
        if (File.Exists(sourceDatabase))
        {
            File.Copy(sourceDatabase, Path.Combine(dataRoot, "workdelta.db"), true);
        }
        var sourceRepositories = Path.Combine(source, "Repositories");
        if (Directory.Exists(sourceRepositories))
        {
            CopyDirectory(sourceRepositories, targetRepositories, CancellationToken.None);
        }
    }

    private static void CopyCurrentForRollback(string dataRoot, string rollback)
    {
        var database = Path.Combine(dataRoot, "workdelta.db");
        if (File.Exists(database))
        {
            File.Copy(database, Path.Combine(rollback, "workdelta.db"), true);
        }
        var repositories = Path.Combine(dataRoot, "Repositories");
        if (Directory.Exists(repositories))
        {
            CopyDirectory(repositories, Path.Combine(rollback, "Repositories"), CancellationToken.None);
        }
    }

    private static void CopyDirectory(string source, string destination, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source))
        {
            cancellationToken.ThrowIfCancellationRequested();
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), true);
        }
        foreach (var directory in Directory.EnumerateDirectories(source))
        {
            cancellationToken.ThrowIfCancellationRequested();
            CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)), cancellationToken);
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
        catch
        {
        }
    }
}
