# Architecture

WorkDelta separates activity metadata from content snapshots.

```text
FileSystemWatcher
      │
      ├── 2-second event coalescing
      ▼
TrackingEngine
      ├── SQLite activity timeline and work sessions
      ├── SHA-256 content-event deduplication
      └── isolated Git mirror snapshots
```

## Local data

```text
%LOCALAPPDATA%\WorkDelta\
├── workdelta.db
└── Repositories\<project-id>\worktree\.git
```

The tracked source folder is never initialized as a Git repository. Only text-like files under 10 MB are
copied into the isolated mirror. Common build output, dependencies, caches, media, databases and temporary
files are excluded.

SQLite owns projects, identities, file activity, work sessions and snapshot metadata. Git owns content
history and diffable checkpoints. This boundary allows a future backup backend without changing work-log
semantics.

## History and restore

The history window reads commit trees from the isolated mirror. Text differences are generated locally and
never sent to a service. Restoring a file or a full checkpoint writes only trackable files back into the
source folder, then creates a new `restore` checkpoint so the operation is reversible.

## Reports and backups

Daily timelines can be queried by local calendar date. Range reports aggregate one or all projects for a
week, month or custom period. A `.workdelta` backup is a ZIP-compatible archive containing a consistent
SQLite backup, the isolated Git repositories and a format manifest. Import occurs before the database is
opened and uses a temporary rollback copy if replacement fails.

## Project preferences

Per-project ignore rules are stored in SQLite and applied by the watcher and the mirror. Rules accept simple
`*` and `?` patterns. Changing a project path restarts its watcher and reconciles the existing mirror.

## Session rule

Nearby changes belong to one session. A gap longer than 15 minutes closes the current session. A single
change still counts as one minute of project activity so it remains visible in summaries.

## Snapshot rule

File events are coalesced before persistence. Content checkpoints are created after two quiet minutes,
with a short first checkpoint for projects that have not yet produced one. FileSystemWatcher overflow
triggers a full mirror reconciliation.
