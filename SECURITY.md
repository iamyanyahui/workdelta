# Security

WorkDelta is local-first. It does not require an account and does not upload project content.

Please report security issues privately through GitHub Security Advisories rather than a public issue.
Do not include real project files, database contents, access tokens, or personal information in a report.

Local application data is stored under `%LOCALAPPDATA%\WorkDelta`. Anyone who can access the Windows
account can potentially access this data. Encryption at rest is not implemented in the initial release.
