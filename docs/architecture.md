# Architecture

WorkDelta separates activity metadata from content snapshots.

```text
FileSystemWatcher
      │
      ├── 2-second event coalescing
      ▼
TrackingEngine
      ├── SQLite activity timeline and work sessions
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

## Session rule

Nearby changes belong to one session. A gap longer than 15 minutes closes the current session. A single
change still counts as one minute of project activity so it remains visible in summaries.

## Snapshot rule

File events are coalesced before persistence. Content checkpoints are created after two quiet minutes,
with a short first checkpoint for projects that have not yet produced one. FileSystemWatcher overflow
triggers a full mirror reconciliation.
