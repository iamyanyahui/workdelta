using Microsoft.Data.Sqlite;
using WorkDelta.Core.Localization;
using WorkDelta.Core.Models;

namespace WorkDelta.Core.Services;

public sealed class WorkDeltaStore
{
    private static readonly TimeSpan SessionGap = TimeSpan.FromMinutes(15);
    private readonly string _connectionString;
    public string DatabasePath { get; }

    public WorkDeltaStore(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        var directory = Path.GetDirectoryName(Path.GetFullPath(databasePath));
        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
        }

        DatabasePath = Path.GetFullPath(databasePath);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            Pooling = true,
            ForeignKeys = true
        }.ToString();
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA journal_mode = WAL;
            PRAGMA foreign_keys = ON;
            PRAGMA busy_timeout = 5000;

            CREATE TABLE IF NOT EXISTS settings (
                key TEXT PRIMARY KEY,
                value TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS projects (
                id TEXT PRIMARY KEY,
                name TEXT NOT NULL,
                path TEXT NOT NULL UNIQUE COLLATE NOCASE,
                created_at_utc TEXT NOT NULL,
                last_activity_at_utc TEXT NULL,
                last_snapshot_at_utc TEXT NULL,
                is_paused INTEGER NOT NULL DEFAULT 0
            );

            CREATE TABLE IF NOT EXISTS work_sessions (
                id TEXT PRIMARY KEY,
                project_id TEXT NOT NULL REFERENCES projects(id) ON DELETE CASCADE,
                started_at_utc TEXT NOT NULL,
                last_activity_at_utc TEXT NOT NULL,
                ended_at_utc TEXT NULL
            );

            CREATE INDEX IF NOT EXISTS ix_sessions_project_time
                ON work_sessions(project_id, started_at_utc DESC);

            CREATE TABLE IF NOT EXISTS activity_events (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                project_id TEXT NOT NULL REFERENCES projects(id) ON DELETE CASCADE,
                session_id TEXT NOT NULL REFERENCES work_sessions(id) ON DELETE CASCADE,
                relative_path TEXT NOT NULL,
                change_kind INTEGER NOT NULL,
                occurred_at_utc TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS ix_events_session ON activity_events(session_id);
            CREATE INDEX IF NOT EXISTS ix_events_project_time
                ON activity_events(project_id, occurred_at_utc DESC);

            CREATE TABLE IF NOT EXISTS snapshots (
                id TEXT PRIMARY KEY,
                project_id TEXT NOT NULL REFERENCES projects(id) ON DELETE CASCADE,
                commit_id TEXT NOT NULL,
                created_at_utc TEXT NOT NULL,
                kind TEXT NOT NULL,
                added_count INTEGER NOT NULL,
                modified_count INTEGER NOT NULL,
                deleted_count INTEGER NOT NULL
            );

            CREATE INDEX IF NOT EXISTS ix_snapshots_project_time
                ON snapshots(project_id, created_at_utc DESC);

            CREATE TABLE IF NOT EXISTS project_preferences (
                project_id TEXT PRIMARY KEY REFERENCES projects(id) ON DELETE CASCADE,
                ignore_patterns TEXT NOT NULL DEFAULT ''
            );
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<AppIdentity> GetOrCreateIdentityAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        var userId = await GetSettingAsync(connection, transaction, "user_id", cancellationToken)
            ?? Guid.NewGuid().ToString("N");
        var displayName = await GetSettingAsync(connection, transaction, "display_name", cancellationToken)
            ?? FriendlyUserName();
        var deviceId = await GetSettingAsync(connection, transaction, "device_id", cancellationToken)
            ?? Guid.NewGuid().ToString("N");
        var deviceName = await GetSettingAsync(connection, transaction, "device_name", cancellationToken)
            ?? Environment.MachineName;

        await SetSettingAsync(connection, transaction, "user_id", userId, cancellationToken);
        await SetSettingAsync(connection, transaction, "display_name", displayName, cancellationToken);
        await SetSettingAsync(connection, transaction, "device_id", deviceId, cancellationToken);
        await SetSettingAsync(connection, transaction, "device_name", deviceName, cancellationToken);
        transaction.Commit();

        return new AppIdentity(userId, displayName, deviceId, deviceName);
    }

    public async Task SetDisplayNameAsync(string displayName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        await using var connection = await OpenAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();
        await SetSettingAsync(connection, transaction, "display_name", displayName.Trim(), cancellationToken);
        transaction.Commit();
    }

    public async Task<string?> GetSettingValueAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT value FROM settings WHERE key = $key;";
        command.Parameters.AddWithValue("$key", key);
        return (string?)await command.ExecuteScalarAsync(cancellationToken);
    }

    public async Task SetSettingValueAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO settings(key, value) VALUES($key, $value)
            ON CONFLICT(key) DO UPDATE SET value = excluded.value;
            """;
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$value", value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<ProjectRecord> AddProjectAsync(string path, CancellationToken cancellationToken = default)
    {
        var normalizedPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        if (!Directory.Exists(normalizedPath))
        {
            throw new DirectoryNotFoundException(CoreText.Format("项目文件夹不存在：{0}", "The project folder does not exist: {0}", normalizedPath));
        }

        var existing = await FindProjectByPathAsync(normalizedPath, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var now = DateTimeOffset.UtcNow;
        var project = new ProjectRecord(
            Guid.NewGuid().ToString("N"),
            new DirectoryInfo(normalizedPath).Name,
            normalizedPath,
            now,
            null,
            null,
            false);

        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO projects(id, name, path, created_at_utc, is_paused)
            VALUES($id, $name, $path, $created, 0);
            """;
        command.Parameters.AddWithValue("$id", project.Id);
        command.Parameters.AddWithValue("$name", project.Name);
        command.Parameters.AddWithValue("$path", project.Path);
        command.Parameters.AddWithValue("$created", ToDb(now));
        await command.ExecuteNonQueryAsync(cancellationToken);
        return project;
    }

    public async Task<IReadOnlyList<ProjectRecord>> GetProjectsAsync(CancellationToken cancellationToken = default)
    {
        var projects = new List<ProjectRecord>();
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, name, path, created_at_utc, last_activity_at_utc,
                   last_snapshot_at_utc, is_paused
            FROM projects
            ORDER BY COALESCE(last_activity_at_utc, created_at_utc) DESC;
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            projects.Add(ReadProject(reader));
        }

        return projects;
    }

    public async Task<ProjectRecord?> GetProjectAsync(string projectId, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, name, path, created_at_utc, last_activity_at_utc,
                   last_snapshot_at_utc, is_paused
            FROM projects WHERE id = $id;
            """;
        command.Parameters.AddWithValue("$id", projectId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadProject(reader) : null;
    }

    public async Task SetProjectPausedAsync(string projectId, bool paused, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE projects SET is_paused = $paused WHERE id = $id;";
        command.Parameters.AddWithValue("$paused", paused ? 1 : 0);
        command.Parameters.AddWithValue("$id", projectId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<ProjectRecord> UpdateProjectAsync(
        string projectId,
        string name,
        string path,
        CancellationToken cancellationToken = default)
    {
        var normalizedPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        if (!Directory.Exists(normalizedPath))
        {
            throw new DirectoryNotFoundException(CoreText.Format("项目文件夹不存在：{0}", "The project folder does not exist: {0}", normalizedPath));
        }

        var trimmedName = name.Trim();
        ArgumentException.ThrowIfNullOrWhiteSpace(trimmedName);
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE projects SET name = $name, path = $path WHERE id = $id;";
        command.Parameters.AddWithValue("$name", trimmedName);
        command.Parameters.AddWithValue("$path", normalizedPath);
        command.Parameters.AddWithValue("$id", projectId);
        await command.ExecuteNonQueryAsync(cancellationToken);
        return await GetProjectAsync(projectId, cancellationToken)
            ?? throw new InvalidOperationException(CoreText.Choose("项目不存在。", "The project does not exist."));
    }

    public async Task DeleteProjectAsync(string projectId, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM projects WHERE id = $id;";
        command.Parameters.AddWithValue("$id", projectId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<string> GetIgnorePatternsAsync(string projectId, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT ignore_patterns FROM project_preferences WHERE project_id = $id;";
        command.Parameters.AddWithValue("$id", projectId);
        return (string?)await command.ExecuteScalarAsync(cancellationToken) ?? string.Empty;
    }

    public async Task SetIgnorePatternsAsync(
        string projectId,
        string patterns,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO project_preferences(project_id, ignore_patterns) VALUES($id, $patterns)
            ON CONFLICT(project_id) DO UPDATE SET ignore_patterns = excluded.ignore_patterns;
            """;
        command.Parameters.AddWithValue("$id", projectId);
        command.Parameters.AddWithValue("$patterns", patterns.Trim());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task RecordChangesAsync(
        ProjectRecord project,
        IReadOnlyCollection<FileChange> changes,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken = default)
    {
        if (changes.Count == 0)
        {
            return;
        }

        await using var connection = await OpenAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        var session = await GetOpenSessionAsync(connection, transaction, project.Id, cancellationToken);
        if (session is not null && occurredAt - session.Value.LastActivity > SessionGap)
        {
            await CloseSessionAsync(connection, transaction, session.Value.Id, session.Value.LastActivity, cancellationToken);
            session = null;
        }

        var sessionId = session?.Id ?? Guid.NewGuid().ToString("N");
        if (session is null)
        {
            await using var insertSession = connection.CreateCommand();
            insertSession.Transaction = transaction;
            insertSession.CommandText = """
                INSERT INTO work_sessions(id, project_id, started_at_utc, last_activity_at_utc)
                VALUES($id, $project, $now, $now);
                """;
            insertSession.Parameters.AddWithValue("$id", sessionId);
            insertSession.Parameters.AddWithValue("$project", project.Id);
            insertSession.Parameters.AddWithValue("$now", ToDb(occurredAt));
            await insertSession.ExecuteNonQueryAsync(cancellationToken);
        }
        else
        {
            await using var updateSession = connection.CreateCommand();
            updateSession.Transaction = transaction;
            updateSession.CommandText = """
                UPDATE work_sessions SET last_activity_at_utc = $now WHERE id = $id;
                """;
            updateSession.Parameters.AddWithValue("$now", ToDb(occurredAt));
            updateSession.Parameters.AddWithValue("$id", sessionId);
            await updateSession.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var change in changes)
        {
            await using var insertEvent = connection.CreateCommand();
            insertEvent.Transaction = transaction;
            insertEvent.CommandText = """
                INSERT INTO activity_events(project_id, session_id, relative_path, change_kind, occurred_at_utc)
                VALUES($project, $session, $path, $kind, $time);
                """;
            insertEvent.Parameters.AddWithValue("$project", project.Id);
            insertEvent.Parameters.AddWithValue("$session", sessionId);
            insertEvent.Parameters.AddWithValue("$path", change.RelativePath);
            insertEvent.Parameters.AddWithValue("$kind", (int)change.Kind);
            insertEvent.Parameters.AddWithValue("$time", ToDb(occurredAt));
            await insertEvent.ExecuteNonQueryAsync(cancellationToken);
        }

        await using var updateProject = connection.CreateCommand();
        updateProject.Transaction = transaction;
        updateProject.CommandText = "UPDATE projects SET last_activity_at_utc = $now WHERE id = $id;";
        updateProject.Parameters.AddWithValue("$now", ToDb(occurredAt));
        updateProject.Parameters.AddWithValue("$id", project.Id);
        await updateProject.ExecuteNonQueryAsync(cancellationToken);
        transaction.Commit();
    }

    public async Task<IReadOnlyList<TimelineEntry>> GetTimelineAsync(
        string projectId,
        DateOnly localDate,
        TimeZoneInfo timeZone,
        CancellationToken cancellationToken = default)
    {
        var startLocal = localDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var endLocal = localDate.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var startUtc = TimeZoneInfo.ConvertTimeToUtc(startLocal, timeZone);
        var endUtc = TimeZoneInfo.ConvertTimeToUtc(endLocal, timeZone);

        var entries = new List<TimelineEntry>();
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT s.id, s.started_at_utc, s.last_activity_at_utc,
                   COUNT(e.id) AS change_count,
                   COUNT(DISTINCT e.relative_path) AS file_count,
                   GROUP_CONCAT(DISTINCT e.relative_path) AS files
            FROM work_sessions s
            LEFT JOIN activity_events e ON e.session_id = s.id
            WHERE s.project_id = $project
              AND s.last_activity_at_utc >= $start
              AND s.started_at_utc < $end
            GROUP BY s.id
            ORDER BY s.started_at_utc DESC;
            """;
        command.Parameters.AddWithValue("$project", projectId);
        command.Parameters.AddWithValue("$start", ToDb(startUtc));
        command.Parameters.AddWithValue("$end", ToDb(endUtc));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var files = reader.IsDBNull(5)
                ? []
                : reader.GetString(5).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            entries.Add(new TimelineEntry(
                reader.GetString(0),
                FromDb(reader.GetString(1)),
                FromDb(reader.GetString(2)),
                reader.GetInt32(3),
                reader.GetInt32(4),
                files));
        }

        return entries;
    }

    public async Task<DashboardSummary> GetDashboardSummaryAsync(
        string projectId,
        DateOnly localDate,
        TimeZoneInfo timeZone,
        CancellationToken cancellationToken = default)
    {
        var timeline = await GetTimelineAsync(projectId, localDate, timeZone, cancellationToken);
        return new DashboardSummary(
            timeline.Count,
            timeline.SelectMany(item => item.Files).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            timeline.Sum(item => item.ChangeCount),
            TimeSpan.FromTicks(timeline.Sum(item => item.ActiveDuration.Ticks)));
    }

    public async Task<IReadOnlyList<ProjectActivityReport>> GetActivityReportsAsync(
        DateOnly startDate,
        DateOnly endDate,
        TimeZoneInfo timeZone,
        string? projectId = null,
        CancellationToken cancellationToken = default)
    {
        if (endDate < startDate)
        {
            (startDate, endDate) = (endDate, startDate);
        }

        var projects = (await GetProjectsAsync(cancellationToken))
            .Where(project => projectId is null || project.Id == projectId)
            .ToArray();
        var reports = new List<ProjectActivityReport>(projects.Length);
        foreach (var project in projects)
        {
            var entries = await GetTimelineRangeAsync(project.Id, startDate, endDate, timeZone, cancellationToken);
            reports.Add(new ProjectActivityReport(
                project.Id,
                project.Name,
                startDate,
                endDate,
                entries.Count,
                entries.SelectMany(item => item.Files).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                entries.Sum(item => item.ChangeCount),
                TimeSpan.FromTicks(entries.Sum(item => item.ActiveDuration.Ticks))));
        }

        return reports
            .Where(report => report.SessionCount > 0 || projectId is not null)
            .OrderByDescending(report => report.ActiveDuration)
            .ToArray();
    }

    public async Task<IReadOnlyList<SnapshotRecord>> GetSnapshotsAsync(
        string projectId,
        CancellationToken cancellationToken = default)
    {
        var snapshots = new List<SnapshotRecord>();
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, project_id, commit_id, created_at_utc, kind,
                   added_count, modified_count, deleted_count
            FROM snapshots WHERE project_id = $project
            ORDER BY created_at_utc DESC;
            """;
        command.Parameters.AddWithValue("$project", projectId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            snapshots.Add(new SnapshotRecord(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                FromDb(reader.GetString(3)),
                reader.GetString(4),
                reader.GetInt32(5),
                reader.GetInt32(6),
                reader.GetInt32(7)));
        }

        return snapshots;
    }

    public async Task CreateDatabaseBackupAsync(string destinationPath, CancellationToken cancellationToken = default)
    {
        var destinationDirectory = Path.GetDirectoryName(Path.GetFullPath(destinationPath));
        if (destinationDirectory is not null)
        {
            Directory.CreateDirectory(destinationDirectory);
        }

        await using var source = await OpenAsync(cancellationToken);
        var destinationBuilder = new SqliteConnectionStringBuilder
        {
            DataSource = destinationPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false
        };
        await using var destination = new SqliteConnection(destinationBuilder.ToString());
        await destination.OpenAsync(cancellationToken);
        source.BackupDatabase(destination);
    }

    private async Task<IReadOnlyList<TimelineEntry>> GetTimelineRangeAsync(
        string projectId,
        DateOnly startDate,
        DateOnly endDate,
        TimeZoneInfo timeZone,
        CancellationToken cancellationToken)
    {
        var startLocal = startDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var endLocal = endDate.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var startUtc = TimeZoneInfo.ConvertTimeToUtc(startLocal, timeZone);
        var endUtc = TimeZoneInfo.ConvertTimeToUtc(endLocal, timeZone);
        var entries = new List<TimelineEntry>();
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT s.id, s.started_at_utc, s.last_activity_at_utc,
                   COUNT(e.id) AS change_count,
                   COUNT(DISTINCT e.relative_path) AS file_count,
                   GROUP_CONCAT(DISTINCT e.relative_path) AS files
            FROM work_sessions s
            LEFT JOIN activity_events e ON e.session_id = s.id
            WHERE s.project_id = $project
              AND s.last_activity_at_utc >= $start
              AND s.started_at_utc < $end
            GROUP BY s.id
            ORDER BY s.started_at_utc DESC;
            """;
        command.Parameters.AddWithValue("$project", projectId);
        command.Parameters.AddWithValue("$start", ToDb(startUtc));
        command.Parameters.AddWithValue("$end", ToDb(endUtc));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var files = reader.IsDBNull(5)
                ? []
                : reader.GetString(5).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            entries.Add(new TimelineEntry(
                reader.GetString(0),
                FromDb(reader.GetString(1)),
                FromDb(reader.GetString(2)),
                reader.GetInt32(3),
                reader.GetInt32(4),
                files));
        }

        return entries;
    }

    public async Task AddSnapshotAsync(ProjectRecord project, SnapshotResult snapshot, string kind, CancellationToken cancellationToken = default)
    {
        if (!snapshot.HasChanges)
        {
            return;
        }

        await using var connection = await OpenAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO snapshots(id, project_id, commit_id, created_at_utc, kind,
                                  added_count, modified_count, deleted_count)
            VALUES($id, $project, $commit, $created, $kind, $added, $modified, $deleted);
            """;
        command.Parameters.AddWithValue("$id", Guid.NewGuid().ToString("N"));
        command.Parameters.AddWithValue("$project", project.Id);
        command.Parameters.AddWithValue("$commit", snapshot.CommitId);
        command.Parameters.AddWithValue("$created", ToDb(snapshot.CreatedAt));
        command.Parameters.AddWithValue("$kind", kind);
        command.Parameters.AddWithValue("$added", snapshot.Added);
        command.Parameters.AddWithValue("$modified", snapshot.Modified);
        command.Parameters.AddWithValue("$deleted", snapshot.Deleted);
        await command.ExecuteNonQueryAsync(cancellationToken);

        await using var update = connection.CreateCommand();
        update.Transaction = transaction;
        update.CommandText = "UPDATE projects SET last_snapshot_at_utc = $created WHERE id = $id;";
        update.Parameters.AddWithValue("$created", ToDb(snapshot.CreatedAt));
        update.Parameters.AddWithValue("$id", project.Id);
        await update.ExecuteNonQueryAsync(cancellationToken);
        transaction.Commit();
    }

    private async Task<ProjectRecord?> FindProjectByPathAsync(string path, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, name, path, created_at_utc, last_activity_at_utc,
                   last_snapshot_at_utc, is_paused
            FROM projects WHERE path = $path COLLATE NOCASE;
            """;
        command.Parameters.AddWithValue("$path", path);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadProject(reader) : null;
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static ProjectRecord ReadProject(SqliteDataReader reader) => new(
        reader.GetString(0),
        reader.GetString(1),
        reader.GetString(2),
        FromDb(reader.GetString(3)),
        reader.IsDBNull(4) ? null : FromDb(reader.GetString(4)),
        reader.IsDBNull(5) ? null : FromDb(reader.GetString(5)),
        reader.GetInt32(6) != 0);

    private static async Task<(string Id, DateTimeOffset LastActivity)?> GetOpenSessionAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string projectId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT id, last_activity_at_utc FROM work_sessions
            WHERE project_id = $project AND ended_at_utc IS NULL
            ORDER BY last_activity_at_utc DESC LIMIT 1;
            """;
        command.Parameters.AddWithValue("$project", projectId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? (reader.GetString(0), FromDb(reader.GetString(1)))
            : null;
    }

    private static async Task CloseSessionAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string sessionId,
        DateTimeOffset endedAt,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "UPDATE work_sessions SET ended_at_utc = $ended WHERE id = $id;";
        command.Parameters.AddWithValue("$ended", ToDb(endedAt));
        command.Parameters.AddWithValue("$id", sessionId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<string?> GetSettingAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string key,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT value FROM settings WHERE key = $key;";
        command.Parameters.AddWithValue("$key", key);
        return (string?)await command.ExecuteScalarAsync(cancellationToken);
    }

    private static async Task SetSettingAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string key,
        string value,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO settings(key, value) VALUES($key, $value)
            ON CONFLICT(key) DO UPDATE SET value = excluded.value;
            """;
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$value", value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string FriendlyUserName()
    {
        var name = Environment.UserName.Trim();
        var slash = Math.Max(name.LastIndexOf('\\'), name.LastIndexOf('/'));
        return slash >= 0 ? name[(slash + 1)..] : name;
    }

    private static string ToDb(DateTimeOffset value) => value.UtcDateTime.ToString("O");
    private static DateTimeOffset FromDb(string value) => DateTimeOffset.Parse(value).ToUniversalTime();
}
