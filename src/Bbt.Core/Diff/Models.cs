using System.Text.Json.Serialization;

namespace Bbt.Core.Diff;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DiffLineType
{
    Context,
    Add,
    Del,
    Meta,
}

public sealed record DiffLine(DiffLineType Type, int? OldLine, int? NewLine, string Text);

public sealed record DiffHunk(
    string Header,
    int OldStart,
    int OldCount,
    int NewStart,
    int NewCount,
    List<DiffLine> Lines);

public sealed record DiffFile(string Path, bool IsBinary, List<DiffHunk> Hunks);

public sealed record PullRequestDiffStats(int FilesChanged, int LinesAdded, int LinesRemoved);

public sealed record PullRequestDiff(
    int PullRequestId,
    string Workspace,
    string Repo,
    List<DiffFile> Files,
    string? RawDiff);
