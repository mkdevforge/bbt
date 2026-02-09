using System.Text.RegularExpressions;

namespace Bbt.Core.Diff;

public static partial class UnifiedDiffParser
{
    public static List<DiffFile> Parse(string diffText)
    {
        var files = new List<DiffFile>();
        if (string.IsNullOrEmpty(diffText))
        {
            return files;
        }

        var lines = diffText.Replace("\r\n", "\n").Split('\n');

        DiffFileBuilder? currentFile = null;
        HunkBuilder? currentHunk = null;

        foreach (var rawLine in lines)
        {
            var line = rawLine;

            if (line.StartsWith("diff --git ", StringComparison.Ordinal))
            {
                CommitCurrent();

                currentFile = new DiffFileBuilder();
                currentHunk = null;

                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (parts.Length >= 4)
                {
                    currentFile.OldPath = ParsePathToken(parts[2]);
                    currentFile.NewPath = ParsePathToken(parts[3]);
                }
                continue;
            }

            if (currentFile is null)
            {
                continue;
            }

            if (line.StartsWith("Binary files ", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("GIT binary patch", StringComparison.OrdinalIgnoreCase))
            {
                currentFile.IsBinary = true;
                currentHunk = null;
                continue;
            }

            if (line.StartsWith("--- ", StringComparison.Ordinal))
            {
                currentFile.OldPath = ParsePathAfterPrefix(line, "--- ");
                continue;
            }

            if (line.StartsWith("+++ ", StringComparison.Ordinal))
            {
                currentFile.NewPath = ParsePathAfterPrefix(line, "+++ ");
                continue;
            }

            var hunkMatch = HunkHeaderRegex().Match(line);
            if (hunkMatch.Success)
            {
                currentHunk = new HunkBuilder(
                    header: line,
                    oldStart: int.Parse(hunkMatch.Groups["oldStart"].Value),
                    oldCount: ParseCountOrDefault(hunkMatch.Groups["oldCount"].Value),
                    newStart: int.Parse(hunkMatch.Groups["newStart"].Value),
                    newCount: ParseCountOrDefault(hunkMatch.Groups["newCount"].Value));
                currentFile.Hunks.Add(currentHunk);
                continue;
            }

            if (currentHunk is null)
            {
                continue;
            }

            if (line.StartsWith("\\ No newline at end of file", StringComparison.Ordinal))
            {
                currentHunk.Lines.Add(new DiffLine(DiffLineType.Meta, null, null, line));
                continue;
            }

            if (line.Length == 0)
            {
                // Trailing newlines (and other blank lines) produce empty elements when splitting.
                // Unified diff hunk lines always have a prefix character, so ignore empty lines here.
                continue;
            }

            var prefix = line[0];
            var content = line.Length > 1 ? line[1..] : string.Empty;

            switch (prefix)
            {
                case ' ':
                    currentHunk.AddContext(content);
                    break;
                case '+':
                    currentHunk.AddAdded(content);
                    break;
                case '-':
                    currentHunk.AddDeleted(content);
                    break;
            }
        }

        CommitCurrent();
        return files;

        void CommitCurrent()
        {
            if (currentFile is null)
            {
                return;
            }

            files.Add(currentFile.Build());
            currentFile = null;
            currentHunk = null;
        }
    }

    private static int ParseCountOrDefault(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return 1;
        }

        return int.TryParse(raw, out var value) ? value : 1;
    }

    private static string ParsePathAfterPrefix(string line, string prefix)
    {
        var path = line[prefix.Length..].Trim();
        if (path.Equals("/dev/null", StringComparison.OrdinalIgnoreCase))
        {
            return path;
        }

        if (path.StartsWith("a/", StringComparison.Ordinal) || path.StartsWith("b/", StringComparison.Ordinal))
        {
            return path[2..];
        }

        return path;
    }

    private static string ParsePathToken(string token)
    {
        var path = token.Trim();
        if (path.StartsWith("a/", StringComparison.Ordinal) || path.StartsWith("b/", StringComparison.Ordinal))
        {
            return path[2..];
        }

        return path;
    }

    private sealed class DiffFileBuilder
    {
        public string? OldPath { get; set; }
        public string? NewPath { get; set; }
        public bool IsBinary { get; set; }
        public List<HunkBuilder> Hunks { get; } = [];

        public DiffFile Build()
        {
            var path = (NewPath is not null && !NewPath.Equals("/dev/null", StringComparison.OrdinalIgnoreCase))
                ? NewPath
                : OldPath;
            path ??= "unknown";

            return new DiffFile(
                Path: path,
                IsBinary: IsBinary,
                Hunks: Hunks.Select(h => h.Build()).ToList());
        }
    }

    private sealed class HunkBuilder
    {
        private int _oldLine;
        private int _newLine;

        public string Header { get; }
        public int OldStart { get; }
        public int OldCount { get; }
        public int NewStart { get; }
        public int NewCount { get; }
        public List<DiffLine> Lines { get; } = [];

        public HunkBuilder(string header, int oldStart, int oldCount, int newStart, int newCount)
        {
            Header = header;
            OldStart = oldStart;
            OldCount = oldCount;
            NewStart = newStart;
            NewCount = newCount;
            _oldLine = oldStart;
            _newLine = newStart;
        }

        public void AddContext(string text)
        {
            Lines.Add(new DiffLine(DiffLineType.Context, _oldLine, _newLine, text));
            _oldLine++;
            _newLine++;
        }

        public void AddAdded(string text)
        {
            Lines.Add(new DiffLine(DiffLineType.Add, null, _newLine, text));
            _newLine++;
        }

        public void AddDeleted(string text)
        {
            Lines.Add(new DiffLine(DiffLineType.Del, _oldLine, null, text));
            _oldLine++;
        }

        public DiffHunk Build()
        {
            return new DiffHunk(Header, OldStart, OldCount, NewStart, NewCount, Lines);
        }
    }

    [GeneratedRegex(@"^@@\s+-(?<oldStart>\d+)(?:,(?<oldCount>\d+))?\s+\+(?<newStart>\d+)(?:,(?<newCount>\d+))?\s+@@", RegexOptions.Compiled)]
    private static partial Regex HunkHeaderRegex();
}
