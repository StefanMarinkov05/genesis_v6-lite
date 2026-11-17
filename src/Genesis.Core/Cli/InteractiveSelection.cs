// FILE-MAP: shared candidate-discovery + numbered-menu helpers behind the CLI's interactive spec/seed pickers.
namespace GenesisV6.Core.Cli;

/// <summary>
/// Pure logic behind "let the person pick one of several candidate files" - used by the
/// CLI so Stefan doesn't have to remember/type an exact --spec path or seed-response
/// filename every run when he has several project-spec.*.json specs or several candidate
/// seed-response.*.json files lying around. Deliberately has ZERO Console I/O in this
/// class - discovery, menu text, and selection parsing are all unit-testable without a
/// real console; Program.cs owns the actual Console.Write/ReadLine calls.
/// </summary>
public static class InteractiveSelection
{
    /// <summary>
    /// Candidate files across the given directories matching the search pattern, deduped
    /// by full path, sorted case-insensitively by name. Missing directories are silently
    /// skipped (no error) so this is safe to call with a search-path list that doesn't
    /// all exist on every machine/repo layout.
    /// </summary>
    public static List<string> FindCandidates(IEnumerable<string> directories, string searchPattern)
    {
        var found = new List<string>();
        foreach (var dir in directories)
        {
            if (!Directory.Exists(dir)) continue;
            found.AddRange(Directory.GetFiles(dir, searchPattern));
        }
        return found.Select(Path.GetFullPath).Distinct()
            .OrderBy(p => Path.GetFileName(p), StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>1-based numbered menu, one candidate per line, paths shown relative to cwd when possible.</summary>
    public static string FormatMenu(IReadOnlyList<string> candidates, string title)
    {
        var lines = new List<string> { title };
        for (var i = 0; i < candidates.Count; i++)
            lines.Add($"  {i + 1}. {RelativeOrFull(candidates[i])}");
        lines.Add("  Enter a number (Enter = 1): ");
        return string.Join(Environment.NewLine, lines);
    }

    public static string RelativeOrFull(string path)
    {
        var cwd = Path.GetFullPath(Directory.GetCurrentDirectory());
        return path.StartsWith(cwd, StringComparison.OrdinalIgnoreCase)
            ? path[(cwd.Length + 1)..].Replace('\\', '/') : path;
    }

    /// <summary>
    /// Parses a 1-based selection against candidates. Blank input selects the first
    /// candidate (Enter = default, matching the menu's own "Enter = 1" prompt). Returns
    /// null on a non-numeric or out-of-range answer so the caller can re-prompt or fail
    /// loud rather than silently picking something the person didn't ask for.
    /// </summary>
    public static string? ParseSelection(string? input, IReadOnlyList<string> candidates)
    {
        if (candidates.Count == 0) return null;
        if (string.IsNullOrWhiteSpace(input)) return candidates[0];
        return int.TryParse(input.Trim(), out var n) && n >= 1 && n <= candidates.Count
            ? candidates[n - 1] : null;
    }
}
