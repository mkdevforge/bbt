using Bbt.Core.Diff;

namespace Bbt.Core.Tests.Diff;

public sealed class PullRequestDiffStatsCalculatorTests
{
    [Fact]
    public void Calculate_CountsFilesAddedAndRemovedLines()
    {
        var files = new List<DiffFile>
        {
            new(
                Path: "src/a.cs",
                IsBinary: false,
                Hunks:
                [
                    new DiffHunk(
                        Header: "@@ -1,2 +1,3 @@",
                        OldStart: 1,
                        OldCount: 2,
                        NewStart: 1,
                        NewCount: 3,
                        Lines:
                        [
                            new DiffLine(DiffLineType.Context, 1, 1, "same"),
                            new DiffLine(DiffLineType.Del, 2, null, "old"),
                            new DiffLine(DiffLineType.Add, null, 2, "new"),
                            new DiffLine(DiffLineType.Add, null, 3, "extra"),
                        ])
                ]),
            new(
                Path: "bin/tool.dat",
                IsBinary: true,
                Hunks: [])
        };

        var stats = PullRequestDiffStatsCalculator.Calculate(files);

        Assert.Equal(2, stats.FilesChanged);
        Assert.Equal(2, stats.LinesAdded);
        Assert.Equal(1, stats.LinesRemoved);
    }
}
