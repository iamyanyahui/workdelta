namespace WorkDelta.Core.Services;

public sealed class PathPolicy
{
    private const long MaximumTrackedFileSize = 10 * 1024 * 1024;

    private static readonly HashSet<string> IgnoredDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", ".svn", ".hg", ".vs", ".idea", ".vscode", "node_modules",
        "bin", "obj", "dist", "build", "target", ".cache", ".next", ".nuxt",
        "coverage", "packages", "vendor", "__pycache__", ".pytest_cache", ".mypy_cache"
    };

    private static readonly HashSet<string> IgnoredExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".tmp", ".temp", ".log", ".lock", ".bak", ".swp", ".swo", ".user",
        ".suo", ".pdb", ".dll", ".exe", ".obj", ".o", ".class", ".pyc",
        ".zip", ".7z", ".rar", ".gz", ".tar", ".png", ".jpg", ".jpeg",
        ".gif", ".webp", ".mp3", ".mp4", ".mov", ".avi", ".pdf", ".db",
        ".sqlite", ".sqlite3"
    };

    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, string[]> _customRules =
        new(StringComparer.OrdinalIgnoreCase);

    public void SetCustomRules(string rootPath, string? patterns)
    {
        var normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootPath));
        var rules = (patterns ?? string.Empty)
            .Split(['\r', '\n', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(rule => !rule.StartsWith('#'))
            .Select(rule => rule.Replace('\\', '/').TrimStart('/'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        _customRules[normalizedRoot] = rules;
    }

    public void RemoveCustomRules(string rootPath) =>
        _customRules.TryRemove(Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootPath)), out _);

    public bool ShouldIgnore(string rootPath, string fullPath)
    {
        string relativePath;
        try
        {
            relativePath = Path.GetRelativePath(rootPath, fullPath);
        }
        catch (ArgumentException)
        {
            return true;
        }

        if (relativePath.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relativePath))
        {
            return true;
        }

        var normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootPath));
        var normalizedRelative = relativePath.Replace('\\', '/');
        if (_customRules.TryGetValue(normalizedRoot, out var rules) &&
            rules.Any(rule => MatchesRule(normalizedRelative, rule)))
        {
            return true;
        }

        var segments = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (segments.Any(segment => IgnoredDirectories.Contains(segment)))
        {
            return true;
        }

        var fileName = Path.GetFileName(relativePath);
        if (fileName.StartsWith("~$", StringComparison.Ordinal) ||
            fileName.EndsWith('~') ||
            fileName.Equals(".DS_Store", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("Thumbs.db", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return IgnoredExtensions.Contains(Path.GetExtension(relativePath));
    }

    private static bool MatchesRule(string relativePath, string rule)
    {
        if (string.IsNullOrWhiteSpace(rule))
        {
            return false;
        }

        var directoryRule = rule.EndsWith('/');
        var candidate = directoryRule ? relativePath + "/" : relativePath;
        if (directoryRule && candidate.StartsWith(rule, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return System.IO.Enumeration.FileSystemName.MatchesSimpleExpression(
                   rule,
                   relativePath,
                   ignoreCase: true) ||
               (!rule.Contains('/') && System.IO.Enumeration.FileSystemName.MatchesSimpleExpression(
                   rule,
                   Path.GetFileName(relativePath),
                   ignoreCase: true));
    }

    public bool IsTrackableFile(string rootPath, string fullPath)
    {
        if (ShouldIgnore(rootPath, fullPath) || !File.Exists(fullPath))
        {
            return false;
        }

        try
        {
            var info = new FileInfo(fullPath);
            return info.Length <= MaximumTrackedFileSize && !LooksBinary(fullPath);
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool LooksBinary(string path)
    {
        Span<byte> buffer = stackalloc byte[4096];
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var read = stream.Read(buffer);
        for (var index = 0; index < read; index++)
        {
            if (buffer[index] == 0)
            {
                return true;
            }
        }

        return false;
    }
}
